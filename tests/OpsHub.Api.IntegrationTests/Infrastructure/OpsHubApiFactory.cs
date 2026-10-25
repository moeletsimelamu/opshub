using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.Testing.Handlers;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using OpsHub.Api.Data;
using OpsHub.Domain.Users;

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

    public static readonly Uri BaseAddress = new("https://localhost");

    public OpsHubApiFactory()
    {
        _keepAlive = new SqliteConnection(_connectionString);
        _keepAlive.Open();

        // HTTPS, so the Secure session cookie is stored and sent like in a real browser.
        ClientOptions.BaseAddress = BaseAddress;
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

        // Every test request comes from the same (unknown) IP; the real limit would trip.
        // RateLimitingTests turns it back down to test the limiter itself.
        builder.UseSetting("Auth:LoginAttemptsPerMinute", "10000");

        builder.ConfigureTestServices(services =>
        {
            // Production re-checks sessions every minute; tests check on every request so
            // "deactivation ends the session" can be verified without waiting.
            services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.Zero);
        });
    }

    /// <summary>
    /// A client that behaves like a browser running the Angular app: it keeps cookies and
    /// copies the XSRF-TOKEN cookie into the X-XSRF-TOKEN header on state-changing requests.
    /// No redirects are followed, so a 302 to a login page would show up as a test failure.
    /// </summary>
    public HttpClient CreateBrowserClient(bool sendXsrfHeader = true)
    {
        var cookies = new CookieContainer();
        var handlers = new List<DelegatingHandler>();
        if (sendXsrfHeader)
        {
            handlers.Add(new XsrfHeaderHandler(cookies));
        }

        handlers.Add(new CookieContainerHandler(cookies));
        return CreateDefaultClient(BaseAddress, [.. handlers]);
    }

    /// <summary>A browser client that is already signed in.</summary>
    public async Task<HttpClient> SignedInClientAsync(string email, string? password = null)
    {
        var client = CreateBrowserClient();
        using var response = await client.LoginAsync(email, password ?? DemoPassword);
        response.EnsureSuccessStatusCode();
        return client;
    }

    /// <summary>
    /// Creates a brand-new user straight through UserManager, for tests that change a user
    /// (lockout, deactivation) and must not affect other tests.
    /// </summary>
    public async Task<TestUser> CreateUserAsync(string role, bool isActive = true)
    {
        var email = $"{role.ToLowerInvariant()}-{Guid.NewGuid():N}@tests.example";
        await using var scope = Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        var user = new AppUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = $"Test {role}",
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow,
        };
        AssertSucceeded(await userManager.CreateAsync(user, DemoPassword));
        AssertSucceeded(await userManager.AddToRoleAsync(user, role));

        return new TestUser(user.Id, email);
    }

    private static void AssertSucceeded(IdentityResult result) =>
        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(e => e.Description)));

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
