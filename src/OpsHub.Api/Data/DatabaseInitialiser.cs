using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpsHub.Api.Data.Seeding;

namespace OpsHub.Api.Data;

internal static class DatabaseInitialiser
{
    /// <summary>
    /// Prepares the database at startup, before the app starts accepting requests:
    /// <list type="number">
    /// <item>applies pending migrations (if configured);</item>
    /// <item>adds missing reference data (roles, starting categories), in every environment;</item>
    /// <item>fills an empty database with demo data (if configured, never in Production);</item>
    /// <item>creates the first administrator (if configured and none exists).</item>
    /// </list>
    /// </summary>
    public static async Task InitialiseDatabaseAsync(this WebApplication app, CancellationToken cancellationToken = default)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;

        var databaseOptions = services.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        var seedOptions = services.GetRequiredService<IOptions<SeedOptions>>().Value;

        if (databaseOptions.ApplyMigrationsOnStartup)
        {
            await services.GetRequiredService<OpsHubDbContext>().Database.MigrateAsync(cancellationToken);
        }

        await services.GetRequiredService<ReferenceDataSeeder>().SeedAsync(cancellationToken);

        if (seedOptions.Enabled)
        {
            if (app.Environment.IsProduction())
            {
                throw new InvalidOperationException("Demo seeding must never run in Production. Set Seed:Enabled to false.");
            }

            await services.GetRequiredService<DevSeeder>().SeedAsync(cancellationToken);
        }

        await services.GetRequiredService<AdminBootstrapper>().RunAsync();
    }
}
