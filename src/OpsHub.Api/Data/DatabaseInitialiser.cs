using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpsHub.Api.Data.Seeding;

namespace OpsHub.Api.Data;

internal static class DatabaseInitialiser
{
    /// <summary>
    /// Applies pending migrations and seeds demo data, depending on configuration.
    /// Runs once at startup, before the app starts accepting requests.
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

        if (seedOptions.Enabled)
        {
            if (app.Environment.IsProduction())
            {
                throw new InvalidOperationException("Demo seeding must never run in Production. Set Seed:Enabled to false.");
            }

            await services.GetRequiredService<DevSeeder>().SeedAsync(cancellationToken);
        }
    }
}
