using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpsHub.Domain.Tickets;
using OpsHub.Domain.Users;

namespace OpsHub.Api.Data.Seeding;

/// <summary>
/// Creates the data every installation needs before anyone can use it, in every environment
/// (unlike the demo data): the three roles, and starting categories so tickets can be filed.
/// Safe to run on every start: it only adds what is missing.
/// </summary>
internal sealed partial class ReferenceDataSeeder(
    OpsHubDbContext db,
    RoleManager<IdentityRole<Guid>> roleManager,
    ILogger<ReferenceDataSeeder> logger)
{
    /// <summary>
    /// General IT support categories. Added only to an empty table, so an installation's own
    /// categories are never overwritten. (There is no category screen yet; see the README.)
    /// </summary>
    public static readonly string[] DefaultCategories =
        ["Hardware", "Software", "Access & Accounts", "Network", "Facilities"];

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        foreach (var role in AppRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var result = await roleManager.CreateAsync(new IdentityRole<Guid>(role));
                if (!result.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"Could not create role {role}: {string.Join("; ", result.Errors.Select(e => e.Description))}");
                }

                LogRoleCreated(logger, role);
            }
        }

        if (!await db.Categories.AnyAsync(cancellationToken))
        {
            db.Categories.AddRange(DefaultCategories.Select(name => new Category { Name = name }));
            await db.SaveChangesAsync(cancellationToken);
            LogCategoriesCreated(logger, DefaultCategories.Length);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Created role {Role}")]
    private static partial void LogRoleCreated(ILogger logger, string role);

    [LoggerMessage(Level = LogLevel.Information, Message = "Created {CategoryCount} default ticket categories")]
    private static partial void LogCategoriesCreated(ILogger logger, int categoryCount);
}
