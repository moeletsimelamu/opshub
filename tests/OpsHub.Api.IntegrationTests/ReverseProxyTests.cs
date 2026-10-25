using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using OpsHub.Api.IntegrationTests.Infrastructure;

namespace OpsHub.Api.IntegrationTests;

/// <summary>
/// The in-memory test server has no network connection, so this sets the connection's IP
/// address from an X-Test-Peer header, before the app's own pipeline runs.
/// </summary>
internal sealed class TestPeerStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (context, nextMiddleware) =>
        {
            if (context.Request.Headers.TryGetValue("X-Test-Peer", out var peer))
            {
                context.Connection.RemoteIpAddress = IPAddress.Parse(peer.ToString());
            }

            await nextMiddleware(context);
        });
        next(app);
    };
}

public class ReverseProxyTests(OpsHubApiFactory baseFactory) : IClassFixture<OpsHubApiFactory>
{
    private const string Proxy = "10.0.0.5";          // inside the configured 10.0.0.0/8
    private const string Stranger = "198.51.100.9";   // a client connecting directly

    private WebApplicationFactory<Program> BehindProxy(int loginLimit = 10_000) => baseFactory.WithWebHostBuilder(b =>
    {
        b.UseSetting("ReverseProxy:Enabled", "true");
        b.UseSetting("ReverseProxy:KnownNetworks:0", "10.0.0.0/8");
        b.UseSetting("Auth:LoginAttemptsPerMinute", loginLimit.ToString(System.Globalization.CultureInfo.InvariantCulture));
        b.ConfigureTestServices(services =>
        {
            services.AddTransient<IStartupFilter, TestPeerStartupFilter>();
            // HSTS is never sent to "localhost" by default; lift that so the scheme test can see it.
            services.Configure<HstsOptions>(o => o.ExcludedHosts.Clear());
        });
    });

    [Fact]
    public async Task ThroughTheProxy_EachClientHasItsOwnLoginLimit()
    {
        using var factory = BehindProxy(loginLimit: 2);
        using var client = factory.CreateDefaultClient(OpsHubApiFactory.BaseAddress);

        var clientA = new[] { await LoginAttempt(client, Proxy, "203.0.113.1"), await LoginAttempt(client, Proxy, "203.0.113.1"), await LoginAttempt(client, Proxy, "203.0.113.1") };
        var clientB = await LoginAttempt(client, Proxy, "203.0.113.2");

        Assert.Equal(HttpStatusCode.TooManyRequests, clientA[2]);
        Assert.NotEqual(HttpStatusCode.TooManyRequests, clientB);
    }

    [Fact]
    public async Task ADirectClient_CannotChooseItsOwnIpAddress()
    {
        using var factory = BehindProxy(loginLimit: 2);
        using var client = factory.CreateDefaultClient(OpsHubApiFactory.BaseAddress);

        // A different made-up X-Forwarded-For on each attempt, to dodge the limit. It is ignored.
        await LoginAttempt(client, Stranger, "203.0.113.10");
        await LoginAttempt(client, Stranger, "203.0.113.11");
        var third = await LoginAttempt(client, Stranger, "203.0.113.12");

        Assert.Equal(HttpStatusCode.TooManyRequests, third);
    }

    [Fact]
    public async Task HttpsAtTheProxy_IsTreatedAsHttps_OnlyWhenTheProxyIsKnown()
    {
        using var factory = BehindProxy();
        using var client = factory.CreateDefaultClient(new Uri("http://localhost"));

        using var viaProxy = await Get(client, peer: Proxy);
        using var fromStranger = await Get(client, peer: Stranger);

        // HSTS is only sent on HTTPS requests, so it shows which scheme the app believed.
        Assert.True(viaProxy.Headers.Contains("Strict-Transport-Security"));
        Assert.False(fromStranger.Headers.Contains("Strict-Transport-Security"));
    }

    [Fact]
    public void EnabledWithoutKnownProxies_StopsTheAppFromStarting()
    {
        using var factory = baseFactory.WithWebHostBuilder(b => b.UseSetting("ReverseProxy:Enabled", "true"));

        var exception = Record.Exception(() => factory.CreateClient());

        Assert.NotNull(exception);
        Assert.Contains("no ReverseProxy:KnownProxies", exception.ToString(), StringComparison.Ordinal);
    }

    private static async Task<HttpStatusCode> LoginAttempt(HttpClient client, string peer, string forwardedFor)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new { email = "a@b.example", password = "x" }),
        };
        request.Headers.Add("X-Test-Peer", peer);
        request.Headers.Add("X-Forwarded-For", forwardedFor);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        return response.StatusCode;
    }

    private static async Task<HttpResponseMessage> Get(HttpClient client, string peer)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("X-Test-Peer", peer);
        request.Headers.Add("X-Forwarded-Proto", "https");
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
