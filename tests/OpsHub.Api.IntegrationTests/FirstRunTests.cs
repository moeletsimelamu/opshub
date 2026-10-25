using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpsHub.Api.Data.Seeding;
using OpsHub.Api.Features.Auth;
using OpsHub.Api.IntegrationTests.Infrastructure;
using OpsHub.Domain.Users;

namespace OpsHub.Api.IntegrationTests;

/// <summary>
/// A new production installation has no demo data. It must still be usable: roles and
/// categories exist, and the first administrator can be created from configuration.
/// </summary>
public class FirstRunTests(OpsHubApiFactory factory) : IClassFixture<OpsHubApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task NewInstallation_HasTheRolesAndDefaultCategories()
    {
        await factory.WithDbAsync(async db =>
        {
            Assert.Equal(AppRoles.All.Order(), await db.Roles.Select(r => r.Name!).OrderBy(n => n).ToListAsync(Ct));
            Assert.Equal(ReferenceDataSeeder.DefaultCategories.Order(), await db.Categories.Select(c => c.Name).OrderBy(n => n).ToListAsync(Ct));
        });
    }

    [Fact]
    public async Task ReferenceData_RunOnEveryStart_AddsNothingTheSecondTime()
    {
        await factory.WithDbAsync<object?>(async (db, services) =>
        {
            await services.GetRequiredService<ReferenceDataSeeder>().SeedAsync(Ct);

            Assert.Equal(AppRoles.All.Count, await db.Roles.CountAsync(Ct));
            Assert.Equal(ReferenceDataSeeder.DefaultCategories.Length, await db.Categories.CountAsync(Ct));
            return null;
        });
    }

    [Fact]
    public async Task Bootstrap_CreatesTheFirstAdministrator_Once()
    {
        using var api = new OpsHubApiFactory();
        using var first = WithBootstrap(api, "first.admin@tests.example", api.DemoPassword);

        using var client = first.CreateClient();
        var admin = await api.SignedInClientAsync("first.admin@tests.example");
        using var me = await admin.GetPathAsync("/api/auth/me");
        Assert.Equal(AppRoles.Administrator, (await me.ReadJsonAsync<CurrentUserResponse>()).Role);

        // Starting again with different settings (or the same ones left in place) creates nobody:
        // an active administrator already exists.
        using var second = WithBootstrap(api, "second.admin@tests.example", api.DemoPassword);
        using var secondClient = second.CreateClient();

        var administrators = await api.WithDbAsync(async (_, services) =>
            await services.GetRequiredService<UserManager<AppUser>>().GetUsersInRoleAsync(AppRoles.Administrator));
        Assert.Equal(["first.admin@tests.example"], administrators.Select(a => a.Email));
    }

    [Theory]
    [InlineData("only.email@tests.example", null, "must be set together")]
    [InlineData("weak.admin@tests.example", "short", "at least 12 characters")]
    public void Bootstrap_WithInvalidSettings_StopsTheAppFromStarting(string? email, string? password, string expected)
    {
        using var api = new OpsHubApiFactory();
        using var factory = WithBootstrap(api, email, password);

        var exception = Record.Exception(() => factory.CreateClient());

        Assert.NotNull(exception);
        Assert.Contains(expected, exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Bootstrap_PasswordIsHashed_NotStored()
    {
        using var api = new OpsHubApiFactory();
        using var factory = WithBootstrap(api, "hashed.admin@tests.example", api.DemoPassword);
        using var client = factory.CreateClient();

        var hash = await api.WithDbAsync(async (db, _) =>
            await db.Users.Where(u => u.Email == "hashed.admin@tests.example").Select(u => u.PasswordHash).SingleAsync(Ct));

        Assert.DoesNotContain(api.DemoPassword, hash, StringComparison.Ordinal);
    }

    private static WebApplicationFactory<Program> WithBootstrap(OpsHubApiFactory api, string? email, string? password) =>
        api.WithWebHostBuilder(b =>
        {
            b.UseSetting("Bootstrap:AdminEmail", email);
            b.UseSetting("Bootstrap:AdminPassword", password);
        });
}

/// <summary>The bootstrap settings are ignored when demo data already includes an administrator.</summary>
public class FirstRunWithDemoDataTests(SeededOpsHubApiFactory factory) : IClassFixture<SeededOpsHubApiFactory>
{
    [Fact]
    public async Task Bootstrap_IsSkipped_WhenAnAdministratorExists()
    {
        using var withBootstrap = factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("Bootstrap:AdminEmail", "extra.admin@tests.example");
            b.UseSetting("Bootstrap:AdminPassword", factory.DemoPassword);
        });
        using var client = withBootstrap.CreateClient();

        using var browser = factory.CreateBrowserClient();
        using var login = await browser.LoginAsync("extra.admin@tests.example", factory.DemoPassword);

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }
}
