using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OpsHub.Api.Auth;
using OpsHub.Api.Common.Results;
using OpsHub.Api.Data;
using OpsHub.Domain.Tickets;

namespace OpsHub.Api.Features.Categories;

/// <summary>
/// Category management for administrators. Categories are never deleted: tickets keep the
/// category they were filed under, so a category that is no longer wanted is deactivated,
/// which only removes it from the ticket forms.
/// </summary>
public sealed partial class CategoryAdminService(OpsHubDbContext db, ICurrentUser currentUser, ILogger<CategoryAdminService> logger)
{
    public async Task<IReadOnlyList<CategoryAdminResponse>> ListAsync(CancellationToken cancellationToken) =>
        await db.Categories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new CategoryAdminResponse(c.Id, c.Name, c.IsActive, c.Tickets.Count))
            .ToListAsync(cancellationToken);

    public async Task<Result<CategoryAdminResponse>> CreateAsync(CreateCategoryRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (await NameTakenAsync(name, exceptId: null, cancellationToken))
        {
            return NameInUse();
        }

        var category = new Category { Name = name };
        db.Categories.Add(category);
        if (!await TrySaveAsync(cancellationToken))
        {
            return NameInUse();
        }

        LogCreated(logger, category.Id, currentUser.Id);
        return new CategoryAdminResponse(category.Id, category.Name, category.IsActive, TicketCount: 0);
    }

    public async Task<Result<CategoryAdminResponse>> UpdateAsync(int id, UpdateCategoryRequest request, CancellationToken cancellationToken)
    {
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (category is null)
        {
            return Error.NotFound("category_not_found", "Category not found.");
        }

        var name = request.Name.Trim();
        if (await NameTakenAsync(name, exceptId: id, cancellationToken))
        {
            return NameInUse();
        }

        var deactivating = category.IsActive && request.IsActive == false;
        if (deactivating && !await db.Categories.AnyAsync(c => c.IsActive && c.Id != id, cancellationToken))
        {
            return Error.Conflict("last_active_category",
                "At least one category must stay active, otherwise nobody could report a problem.");
        }

        category.Name = name;
        category.IsActive = request.IsActive!.Value;
        if (!await TrySaveAsync(cancellationToken))
        {
            return NameInUse();
        }

        LogUpdated(logger, category.Id, category.IsActive, currentUser.Id);
        var ticketCount = await db.Tickets.CountAsync(t => t.CategoryId == id, cancellationToken);
        return new CategoryAdminResponse(category.Id, category.Name, category.IsActive, ticketCount);
    }

    // "Network" and "network" would look like the same category in the forms. SQLite's unique
    // index compares exactly, so this check compares with the NOCASE collation instead.
    private Task<bool> NameTakenAsync(string name, int? exceptId, CancellationToken cancellationToken) =>
        db.Categories.AnyAsync(c => c.Id != exceptId && EF.Functions.Collate(c.Name, "NOCASE") == name, cancellationToken);

    /// <summary>The unique index still guards against two administrators saving the same name at once.</summary>
    private async Task<bool> TrySaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException e) when (e.InnerException is SqliteException { SqliteErrorCode: 19 }) // SQLITE_CONSTRAINT
        {
            return false;
        }
    }

    private static Error NameInUse() => Error.Conflict("category_name_in_use", "A category with this name already exists.");

    [LoggerMessage(Level = LogLevel.Information, Message = "Category {CategoryId} created by {UserId}")]
    private static partial void LogCreated(ILogger logger, int categoryId, Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Category {CategoryId} updated by {UserId} (active: {IsActive})")]
    private static partial void LogUpdated(ILogger logger, int categoryId, bool isActive, Guid userId);
}
