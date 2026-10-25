using System.ComponentModel.DataAnnotations;
using OpsHub.Api.Common;
using OpsHub.Domain.Users;

namespace OpsHub.Api.Features.Users;

public sealed record UserResponse(
    Guid Id,
    string Email,
    string DisplayName,
    string Role,
    bool IsActive,
    DateTime CreatedAt);

/// <summary>GET /api/users?search=&amp;role=&amp;isActive=&amp;page=&amp;pageSize=</summary>
public sealed class UserListQuery : PageQuery
{
    /// <summary>Matches display name or email, case-insensitively.</summary>
    [MaxLength(100)]
    public string? Search { get; set; }

    [AllowedValues(null, AppRoles.Administrator, AppRoles.SupportAgent, AppRoles.Requester)]
    public string? Role { get; set; }

    public bool? IsActive { get; set; }
}

public sealed record CreateUserRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; init; } = string.Empty;

    [Required, MaxLength(AppUser.DisplayNameMaxLength)]
    public string DisplayName { get; init; } = string.Empty;

    [Required, AllowedValues(AppRoles.Administrator, AppRoles.SupportAgent, AppRoles.Requester)]
    public string Role { get; init; } = string.Empty;

    /// <summary>Initial password chosen by the administrator; the user can change it after signing in.</summary>
    [Required, MaxLength(128)]
    public string Password { get; init; } = string.Empty;
}

public sealed record UpdateUserRequest
{
    [Required, MaxLength(AppUser.DisplayNameMaxLength)]
    public string DisplayName { get; init; } = string.Empty;

    [Required, AllowedValues(AppRoles.Administrator, AppRoles.SupportAgent, AppRoles.Requester)]
    public string Role { get; init; } = string.Empty;

    public bool IsActive { get; init; }
}
