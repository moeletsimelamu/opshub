using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using OpsHub.Api.IntegrationTests.Infrastructure;
using OpsHub.Domain.Users;

namespace OpsHub.Api.IntegrationTests;

/// <summary>The API with a (small, fake) Angular production build in its web root.</summary>
public sealed class FrontendFixture : IDisposable
{
    public string WebRoot { get; } = Directory.CreateTempSubdirectory("opshub-wwwroot-").FullName;

    public OpsHubApiFactory Api { get; } = new();

    public WebApplicationFactory<Program> App { get; }

    public FrontendFixture()
    {
        File.WriteAllText(Path.Combine(WebRoot, "index.html"), "<!doctype html><title>OpsHub</title><app-root></app-root>");
        File.WriteAllText(Path.Combine(WebRoot, "main-ABCD1234.js"), "console.log('app');");
        File.WriteAllText(Path.Combine(WebRoot, "favicon.svg"), "<svg xmlns=\"http://www.w3.org/2000/svg\"/>");

        App = Api.WithWebHostBuilder(b => b.UseWebRoot(WebRoot));
    }

    public void Dispose()
    {
        App.Dispose();
        Api.Dispose();
        Directory.Delete(WebRoot, recursive: true);
    }
}

public class FrontendHostingTests(FrontendFixture fixture) : IClassFixture<FrontendFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("/")]
    [InlineData("/tickets/12")]          // a bookmarked screen: the Angular router shows it
    [InlineData("/admin/users")]
    public async Task PageAddresses_ServeTheAngularApp_WithoutSigningIn(string path)
    {
        using var client = fixture.App.CreateClient();

        using var response = await client.GetPathAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("<app-root>", await response.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ThePage_GetsTheAppPolicy_AndIsRevalidatedOnEveryVisit()
    {
        using var client = fixture.App.CreateClient();

        using var response = await client.GetPathAsync("/");

        var csp = string.Join(",", response.Headers.GetValues("Content-Security-Policy"));
        Assert.Contains("script-src 'self'", csp, StringComparison.Ordinal);
        Assert.Contains("frame-ancestors 'none'", csp, StringComparison.Ordinal);
        Assert.DoesNotContain("unsafe-eval", csp, StringComparison.Ordinal);
        Assert.True(response.Headers.CacheControl?.NoCache);
    }

    [Fact]
    public async Task HashedFiles_AreCachedForAYear()
    {
        using var client = fixture.App.CreateClient();

        using var response = await client.GetPathAsync("/main-ABCD1234.js");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(TimeSpan.FromDays(365), response.Headers.CacheControl?.MaxAge);
        Assert.Contains("immutable", response.Headers.CacheControl?.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnhashedFiles_AreRevalidated()
    {
        using var client = fixture.App.CreateClient();

        using var response = await client.GetPathAsync("/favicon.svg");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoCache);
    }

    [Fact]
    public async Task MissingFiles_AreNotAnsweredWithThePage()
    {
        using var client = fixture.App.CreateClient();

        using var response = await client.GetPathAsync("/main-MISSING1.js");

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UnknownApiAddresses_StayApiErrors()
    {
        using var anonymous = fixture.App.CreateClient();
        using var signedOut = await anonymous.GetPathAsync("/api/does-not-exist");

        var user = await fixture.Api.CreateUserAsync(AppRoles.Requester);
        using var signedIn = await SignInAndGetAsync(user.Email, "/api/does-not-exist");

        Assert.Equal(HttpStatusCode.Unauthorized, signedOut.StatusCode);
        Assert.Equal("application/problem+json", signedOut.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.NotFound, signedIn.StatusCode);
        Assert.Equal("application/problem+json", signedIn.Content.Headers.ContentType?.MediaType);
    }

    private async Task<HttpResponseMessage> SignInAndGetAsync(string email, string path)
    {
        var cookies = new CookieContainer();
        using var client = fixture.App.CreateDefaultClient(
            OpsHubApiFactory.BaseAddress,
            new XsrfHeaderHandler(cookies),
            new Microsoft.AspNetCore.Mvc.Testing.Handlers.CookieContainerHandler(cookies));
        using var login = await client.LoginAsync(email, fixture.Api.DemoPassword);
        login.EnsureSuccessStatusCode();
        return await client.GetPathAsync(path);
    }
}

/// <summary>Development and the tests have no web root: no page is served and nothing changes.</summary>
public class NoFrontendTests(OpsHubApiFactory factory) : IClassFixture<OpsHubApiFactory>
{
    [Fact]
    public async Task WithoutABuild_TheApiServesNoPage()
    {
        using var client = factory.CreateBrowserClient();

        using var response = await client.GetPathAsync("/");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
