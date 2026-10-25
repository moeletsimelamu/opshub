using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using OpsHub.Api.Data;

namespace OpsHub.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Boots the real API in memory (no network port) against a private in-memory SQLite
/// database, with the real migrations applied. Each factory instance gets its own database.
/// </summary>
public class OpsHubApiFactory : WebApplicationFactory<Program>
{
    // "Mode=Memory;Cache=Shared" makes a named in-memory database that every connection
    // in this process can share. It lives only while at least one connection is open.
    private readonly string _connectionString =
        $"Data Source=opshub-tests-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";

    private readonly SqliteConnection _keepAlive;

    public OpsHubApiFactory()
    {
        _keepAlive = new SqliteConnection(_connectionString);
        _keepAlive.Open();
    }

    /// <summary>Override to turn on demo seeding for a test class.</summary>
    protected virtual bool SeedDemoData => false;

    /// <summary>Generated per test run; a test-only value that is never stored anywhere.</summary>
    public string DemoPassword { get; } = $"test-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:OpsHub", _connectionString);
        builder.UseSetting("Database:ApplyMigrationsOnStartup", "true");
        builder.UseSetting("Seed:Enabled", SeedDemoData ? "true" : "false");

        if (SeedDemoData)
        {
            builder.UseSetting("Seed:DemoPassword", DemoPassword);
        }
    }

    /// <summary>Runs <paramref name="action"/> with a fresh DbContext, like a single HTTP request would.</summary>
    public async Task<T> WithDbAsync<T>(Func<OpsHubDbContext, IServiceProvider, Task<T>> action)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OpsHubDbContext>();
        return await action(db, scope.ServiceProvider);
    }

    public Task WithDbAsync(Func<OpsHubDbContext, Task> action) =>
        WithDbAsync<object?>(async (db, _) =>
        {
            await action(db);
            return null;
        });

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _keepAlive.Dispose();
        }
    }
}

/// <summary>An API instance whose database has been filled with the fictional demo data.</summary>
public sealed class SeededOpsHubApiFactory : OpsHubApiFactory
{
    protected override bool SeedDemoData => true;
}
