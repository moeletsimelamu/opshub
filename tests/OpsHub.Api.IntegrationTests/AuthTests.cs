using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OpsHub.Api.Data.Seeding;
using OpsHub.Api.Features.Auth;
using OpsHub.Api.IntegrationTests.Infrastructure;
using OpsHub.Domain.Users;

namespace OpsHub.Api.IntegrationTests;

public class AuthTests(SeededOpsHubApiFactory factory) : IClassFixture<SeededOpsHubApiFactory>
{
    private static readonly string AdminEmail = DemoData.EmailFor("lerato");

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsUserAndSetsHardenedSessionCookie()
    {
        using var client = factory.CreateBrowserClient();

        using var response = await client.LoginAsync(AdminEmail, factory.DemoPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await response.ReadJsonAsync<CurrentUserResponse>();
        Assert.Equal(AdminEmail, me.Email);
        Assert.Equal("Lerato Dlamini", me.DisplayName);
        Assert.Equal(AppRoles.Administrator, me.Role);

        var sessionCookie = Assert.Single(
            response.Headers.GetValues("Set-Cookie"),
            c => c.StartsWith("__Host-opshub-session=", StringComparison.Ordinal));
        Assert.Contains("httponly", sessionCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", sessionCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", sessionCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401ProblemDetails()
    {
        using var client = factory.CreateBrowserClient();

        using var response = await client.LoginAsync(AdminEmail, "not-the-right-password");

        var problem = await response.AssertProblemAsync(HttpStatusCode.Unauthorized);
        Assert.Equal("Sign-in failed", problem.Title);
    }

    [Fact]
    public async Task Login_WithUnknownEmail_LooksIdenticalToWrongPassword()
    {
        using var client = factory.CreateBrowserClient();

        using var unknown = await client.LoginAsync("nobody@marula.example", "whatever-password-1");
        using var wrong = await client.LoginAsync(AdminEmail, "whatever-password-1");

        var unknownProblem = await unknown.AssertProblemAsync(HttpStatusCode.Unauthorized);
        var wrongProblem = await wrong.AssertProblemAsync(HttpStatusCode.Unauthorized);
        Assert.Equal(wrongProblem.Title, unknownProblem.Title);
        Assert.Equal(wrongProblem.Detail, unknownProblem.Detail);
    }

    [Fact]
    public async Task Login_WithoutXsrfHeader_IsRejected()
    {
        using var client = factory.CreateBrowserClient(sendXsrfHeader: false);

        using var response = await client.LoginAsync(AdminEmail, factory.DemoPassword);

        await response.AssertProblemAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_WithInvalidEmailFormat_Returns400WithFieldError()
    {
        using var client = factory.CreateBrowserClient();

        using var response = await client.LoginAsync("not-an-email", factory.DemoPassword);

        var problem = await response.AssertProblemAsync(HttpStatusCode.BadRequest);
        Assert.Contains("email", problem.Errors.Keys);
    }

    [Fact]
    public async Task Login_AfterFiveWrongPasswords_LocksAccountEvenForCorrectPassword()
    {
        var user = await factory.CreateUserAsync(AppRoles.Requester);
        using var client = factory.CreateBrowserClient();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var failed = await client.LoginAsync(user.Email, "wrong-password-123");
            Assert.Equal(HttpStatusCode.Unauthorized, failed.StatusCode);
        }

        using var correct = await client.LoginAsync(user.Email, factory.DemoPassword);

        Assert.Equal(HttpStatusCode.Unauthorized, correct.StatusCode);
    }

    [Fact]
    public async Task Login_DeactivatedUser_IsRejected()
    {
        var user = await factory.CreateUserAsync(AppRoles.SupportAgent, isActive: false);
        using var client = factory.CreateBrowserClient();

        using var response = await client.LoginAsync(user.Email, factory.DemoPassword);

        await response.AssertProblemAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_WithoutSession_Returns401ProblemDetails_NotARedirect()
    {
        // Cookie authentication would normally answer with a 302 redirect to a login page;
        // an API client needs a 401 it can react to. (The fallback "must be signed in"
        // policy itself is proven by HealthEndpointTests.UnknownRoute_Anonymous_Returns401ProblemDetails.)
        using var client = factory.CreateBrowserClient();

        using var response = await client.GetPathAsync("/api/auth/me");

        await response.AssertProblemAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UnknownRoute_SignedIn_Returns404ProblemDetails()
    {
        using var client = await factory.SignedInClientAsync(DemoData.EmailFor("thandi"));

        using var response = await client.GetPathAsync("/api/does-not-exist");

        await response.AssertProblemAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Me_AfterLogin_ReturnsSignedInUser()
    {
        using var client = await factory.SignedInClientAsync(DemoData.EmailFor("thandi"));

        using var response = await client.GetPathAsync("/api/auth/me");

        var me = await response.ReadJsonAsync<CurrentUserResponse>();
        Assert.Equal(AppRoles.Requester, me.Role);
    }

    [Fact]
    public async Task Logout_EndsTheSession()
    {
        using var client = await factory.SignedInClientAsync(DemoData.EmailFor("sipho"));

        using var logout = await client.PostJsonAsync("/api/auth/logout", new { });
        using var me = await client.GetPathAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_WithWrongCurrentPassword_Returns400OnCurrentPasswordField()
    {
        var user = await factory.CreateUserAsync(AppRoles.Requester);
        using var client = await factory.SignedInClientAsync(user.Email);

        using var response = await client.PostJsonAsync("/api/auth/change-password",
            new { currentPassword = "not-my-password", newPassword = "a-brand-new-password" });

        var problem = await response.AssertProblemAsync(HttpStatusCode.BadRequest);
        Assert.Contains("currentPassword", problem.Errors.Keys);
    }

    [Fact]
    public async Task ChangePassword_TooShort_Returns400OnNewPasswordField()
    {
        var user = await factory.CreateUserAsync(AppRoles.Requester);
        using var client = await factory.SignedInClientAsync(user.Email);

        using var response = await client.PostJsonAsync("/api/auth/change-password",
            new { currentPassword = factory.DemoPassword, newPassword = "short" });

        var problem = await response.AssertProblemAsync(HttpStatusCode.BadRequest);
        Assert.Contains("newPassword", problem.Errors.Keys);
    }

    [Fact]
    public async Task ChangePassword_Succeeds_KeepsThisSession_EndsOthers_AndReplacesPassword()
    {
        var user = await factory.CreateUserAsync(AppRoles.Requester);
        using var thisBrowser = await factory.SignedInClientAsync(user.Email);
        using var otherBrowser = await factory.SignedInClientAsync(user.Email);
        const string newPassword = "correct-horse-battery-staple";

        using var change = await thisBrowser.PostJsonAsync("/api/auth/change-password",
            new { currentPassword = factory.DemoPassword, newPassword });
        Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);

        using var thisMe = await thisBrowser.GetPathAsync("/api/auth/me");
        using var otherMe = await otherBrowser.GetPathAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, thisMe.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, otherMe.StatusCode);

        using var fresh = factory.CreateBrowserClient();
        using var oldLogin = await fresh.LoginAsync(user.Email, factory.DemoPassword);
        using var newLogin = await fresh.LoginAsync(user.Email, newPassword);
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
    }

    [Fact]
    public async Task FailedSignIns_AreCountedInTheDatabase()
    {
        var user = await factory.CreateUserAsync(AppRoles.Requester);
        using var client = factory.CreateBrowserClient();

        using var failed = await client.LoginAsync(user.Email, "wrong-password-123");

        var failures = await factory.WithDbAsync(async (_, services) =>
        {
            var userManager = services.GetRequiredService<UserManager<AppUser>>();
            var stored = await userManager.FindByIdAsync(user.Id.ToString());
            return stored!.AccessFailedCount;
        });
        Assert.Equal(1, failures);
    }
}

public class LoginRateLimitTests(OpsHubApiFactory baseFactory) : IClassFixture<OpsHubApiFactory>
{
    [Fact]
    public async Task Login_BeyondPerMinuteLimit_Returns429()
    {
        using var factory = baseFactory.WithWebHostBuilder(b => b.UseSetting("Auth:LoginAttemptsPerMinute", "3"));
        using var client = factory.CreateDefaultClient(OpsHubApiFactory.BaseAddress);

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 4; i++)
        {
            // No XSRF token: the first three are rejected with 400, which still counts.
            using var response = await client.PostJsonAsync("/api/auth/login", new { email = "a@b.example", password = "x" });
            statuses.Add(response.StatusCode);
        }

        Assert.Equal([HttpStatusCode.BadRequest, HttpStatusCode.BadRequest, HttpStatusCode.BadRequest, HttpStatusCode.TooManyRequests], statuses);
    }
}
