using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpsHub.Api.Auth;
using OpsHub.Api.Common;
using OpsHub.Api.Common.Results;
using OpsHub.Api.Data;
using OpsHub.Domain.Users;

namespace OpsHub.Api.Features.Users;

/// <summary>
/// User administration. Only reachable through AdminOnly endpoints, but the rules that
/// protect the system (e.g. an admin cannot lock themselves out) live here, not in the UI.
/// Every user has exactly one role.
/// </summary>
public sealed partial class UserAdminService(
    OpsHubDbContext db,
    UserManager<AppUser> userManager,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    ILogger<UserAdminService> logger)
{
    public async Task<PagedResult<UserResponse>> ListAsync(UserListQuery query, CancellationToken cancellationToken)
    {
        var users =
            from user in db.Users
            join userRole in db.UserRoles on user.Id equals userRole.UserId
            join role in db.Roles on userRole.RoleId equals role.Id
            select new { User = user, Role = role.Name! };

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            // LIKE is case-insensitive in SQLite; string.Contains would translate to instr(), which is not.
            var pattern = LikePattern.Contains(query.Search.Trim());
            var escape = LikePattern.EscapeCharacter.ToString();
            users = users.Where(x =>
                EF.Functions.Like(x.User.DisplayName, pattern, escape) ||
                EF.Functions.Like(x.User.Email!, pattern, escape));
        }

        if (query.Role is not null)
        {
            users = users.Where(x => x.Role == query.Role);
        }

        if (query.IsActive is not null)
        {
            users = users.Where(x => x.User.IsActive == query.IsActive);
        }

        var totalCount = await users.CountAsync(cancellationToken);
        var items = await users
            .OrderBy(x => x.User.DisplayName)
            .ThenBy(x => x.User.Id)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(x => new UserResponse(x.User.Id, x.User.Email!, x.User.DisplayName, x.Role, x.User.IsActive, x.User.CreatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<UserResponse>(items, query.Page, query.PageSize, totalCount);
    }

    public async Task<Result<UserResponse>> GetAsync(Guid id)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        return user is null ? UserNotFound() : await ToResponseAsync(user);
    }

    public async Task<Result<UserResponse>> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();
        var user = new AppUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true, // No email verification in this demo (see README limitations).
            DisplayName = request.DisplayName.Trim(),
            IsActive = true,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
        };

        // Creating the user and assigning the role are two writes; the transaction makes
        // sure we never end up with a user who has no role.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var created = await userManager.CreateAsync(user, request.Password);
        if (!created.Succeeded)
        {
            return MapIdentityErrors(created.Errors);
        }

        var roleAdded = await userManager.AddToRoleAsync(user, request.Role);
        if (!roleAdded.Succeeded)
        {
            return MapIdentityErrors(roleAdded.Errors);
        }

        await transaction.CommitAsync(cancellationToken);
        LogUserCreated(logger, currentUser.Id, user.Id, request.Role);

        return new UserResponse(user.Id, user.Email, user.DisplayName, request.Role, user.IsActive, user.CreatedAt);
    }

    public async Task<Result<UserResponse>> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return UserNotFound();
        }

        var currentRole = (await userManager.GetRolesAsync(user)).FirstOrDefault();

        // Because only active administrators can call this, and they cannot demote or
        // deactivate themselves, at least one active administrator always remains.
        if (user.Id == currentUser.Id)
        {
            if (!request.IsActive)
            {
                return Error.Conflict("cannot_deactivate_self", "You cannot deactivate your own account.");
            }

            if (request.Role != AppRoles.Administrator)
            {
                return Error.Conflict("cannot_demote_self", "You cannot remove your own Administrator role.");
            }
        }

        var roleChanged = currentRole != request.Role;
        var deactivated = user.IsActive && !request.IsActive;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        if (roleChanged)
        {
            if (currentRole is not null)
            {
                var removed = await userManager.RemoveFromRoleAsync(user, currentRole);
                if (!removed.Succeeded)
                {
                    return MapIdentityErrors(removed.Errors);
                }
            }

            var added = await userManager.AddToRoleAsync(user, request.Role);
            if (!added.Succeeded)
            {
                return MapIdentityErrors(added.Errors);
            }
        }

        user.DisplayName = request.DisplayName.Trim();
        user.IsActive = request.IsActive;

        // A new security stamp invalidates the user's existing sessions at the next
        // validation, so a role change or deactivation takes effect within a minute
        // instead of when their cookie expires.
        var saved = roleChanged || deactivated
            ? await userManager.UpdateSecurityStampAsync(user)
            : await userManager.UpdateAsync(user);
        if (!saved.Succeeded)
        {
            return MapIdentityErrors(saved.Errors);
        }

        await transaction.CommitAsync(cancellationToken);
        LogUserUpdated(logger, currentUser.Id, user.Id, request.Role, request.IsActive);

        return new UserResponse(user.Id, user.Email!, user.DisplayName, request.Role, user.IsActive, user.CreatedAt);
    }

    private async Task<UserResponse> ToResponseAsync(AppUser user)
    {
        var role = (await userManager.GetRolesAsync(user)).FirstOrDefault() ?? string.Empty;
        return new UserResponse(user.Id, user.Email!, user.DisplayName, role, user.IsActive, user.CreatedAt);
    }

    private static Error UserNotFound() => Error.NotFound("user_not_found", "User not found.");

    /// <summary>Turns Identity's error list into a 409 (duplicate) or a 400 with per-field messages.</summary>
    private static Error MapIdentityErrors(IEnumerable<IdentityError> errors)
    {
        var list = errors.ToList();

        if (list.Exists(e => e.Code is nameof(IdentityErrorDescriber.DuplicateEmail) or nameof(IdentityErrorDescriber.DuplicateUserName)))
        {
            return Error.Conflict("email_in_use", "An account with this email address already exists.");
        }

        if (list.Exists(e => e.Code == nameof(IdentityErrorDescriber.ConcurrencyFailure)))
        {
            return Error.Conflict("concurrency_conflict", "This user was changed by someone else. Reload and try again.");
        }

        var fields = list
            .GroupBy(e => e.Code.StartsWith("Password", StringComparison.Ordinal) ? "password"
                : e.Code.Contains("Email", StringComparison.Ordinal) || e.Code.Contains("UserName", StringComparison.Ordinal) ? "email"
                : "user")
            .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());

        return new Error(ErrorType.Validation, "validation_failed", "One or more fields are invalid.", fields);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Administrator {AdminId} created user {UserId} with role {Role}")]
    private static partial void LogUserCreated(ILogger logger, Guid adminId, Guid userId, string role);

    [LoggerMessage(Level = LogLevel.Information, Message = "Administrator {AdminId} updated user {UserId}: role {Role}, active {IsActive}")]
    private static partial void LogUserUpdated(ILogger logger, Guid adminId, Guid userId, string role, bool isActive);
}
