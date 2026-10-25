using System.Net;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using OpsHub.Api.IntegrationTests.Infrastructure;

namespace OpsHub.Api.IntegrationTests;

public class SecurityHeadersTests(OpsHubApiFactory factory) : IClassFixture<OpsHubApiFactory>
{
    [Theory]
    [InlineData("/health")]                 // success
    [InlineData("/api/auth/me")]            // 401 ProblemDetails
    [InlineData("/api/does-not-exist")]     // unknown route
    public async Task EveryResponse_CarriesTheDefensiveHeaders(string path)
    {
        using var client = factory.CreateBrowserClient();

        using var response = await client.GetPathAsync(path);

        Assert.Equal("nosniff", Header(response, "X-Content-Type-Options"));
        Assert.Equal("no-referrer", Header(response, "Referrer-Policy"));
        Assert.Equal("DENY", Header(response, "X-Frame-Options"));
        Assert.Contains("default-src 'none'", Header(response, "Content-Security-Policy"), StringComparison.Ordinal);
        Assert.Contains("frame-ancestors 'none'", Header(response, "Content-Security-Policy"), StringComparison.Ordinal);
        Assert.Contains("camera=()", Header(response, "Permissions-Policy"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task OutsideDevelopment_HttpsResponses_SendHsts()
    {
        // The test host runs as "Testing" (not Development) over https://localhost. ASP.NET Core
        // never sends HSTS to localhost by default (it would stick in developers' browsers), so
        // this test lifts that exclusion to prove the middleware is wired up.
        using var customised = factory.WithWebHostBuilder(b =>
            b.ConfigureTestServices(s => s.Configure<HstsOptions>(o => o.ExcludedHosts.Clear())));
        using var client = customised.CreateClient();

        using var response = await client.GetPathAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("max-age=15552000", Header(response, "Strict-Transport-Security")); // 180 days
    }

    private static string Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) || response.Content.Headers.TryGetValues(name, out values)
            ? string.Join(",", values)
            : string.Empty;
}
