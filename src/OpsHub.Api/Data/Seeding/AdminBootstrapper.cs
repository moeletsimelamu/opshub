using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using OpsHub.Domain.Users;

namespace OpsHub.Api.Data.Seeding;

/// <summary>
/// Creates the first administrator from <see cref="BootstrapOptions"/> when the installation
/// has no active administrator. Once one exists the settings are ignored, so leaving them in
/// place can't create a second account or reset a password.
/// </summary>
internal sealed partial class AdminBootstrapper(
    UserManager<AppUser> userManager,
    TimeProvider timeProvider,
    IOptions<BootstrapOptions> options,
    ILogger<AdminBootstrapper> logger)
{
    public async Task RunAsync()
    {
        var settings = options.Value;
        var hasEmail = !string.IsNullOrWhiteSpace(settings.AdminEmail);
        var hasPassword = !string.IsNullOrWhiteSpace(settings.AdminPassword);

        if (!hasEmail && !hasPassword)
        {
            return;
        }

        if (hasEmail != hasPassword)
        {
            throw new InvalidOperationException("Bootstrap:AdminEmail and Bootstrap:AdminPassword must be set together.");
        }

        var administrators = await userManager.GetUsersInRoleAsync(AppRoles.Administrator);
        if (administrators.Any(a => a.IsActive))
        {
            LogSkipped(logger);
            return;
        }

        var email = settings.AdminEmail!.Trim();
        if (await userManager.FindByEmailAsync(email) is not null)
        {
            // Turning an existing account into an administrator from configuration would let
            // whoever controls the settings take over that account; do it in the database instead.
            throw new InvalidOperationException(
                "Bootstrap: an account with Bootstrap:AdminEmail already exists but is not an active administrator.");
        }

        var user = new AppUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = settings.AdminDisplayName.Trim(),
            IsActive = true,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
        };

        // The password policy (at least 12 characters) applies here too.
        EnsureSucceeded(await userManager.CreateAsync(user, settings.AdminPassword!));
        EnsureSucceeded(await userManager.AddToRoleAsync(user, AppRoles.Administrator));

        LogCreated(logger, email);
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Bootstrap: could not create the administrator: {string.Join("; ", result.Errors.Select(e => e.Description))}");
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Bootstrap skipped: an active administrator already exists")]
    private static partial void LogSkipped(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Bootstrap created administrator {Email}. Remove Bootstrap__AdminPassword from the configuration now")]
    private static partial void LogCreated(ILogger logger, string email);
}
