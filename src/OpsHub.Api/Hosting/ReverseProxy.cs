using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

namespace OpsHub.Api.Hosting;

/// <summary>
/// Bound from the "ReverseProxy" configuration section. In production, a proxy or load
/// balancer usually handles HTTPS and forwards plain HTTP to the app. The app then sees the
/// proxy's IP address and "http"; the proxy reports the real ones in X-Forwarded-For and
/// X-Forwarded-Proto. Those headers are only believed when they come from a known proxy,
/// otherwise any client could fake its IP address and dodge the login rate limit.
/// </summary>
public sealed class ReverseProxyOptions
{
    public const string SectionName = "ReverseProxy";

    public bool Enabled { get; set; }

    /// <summary>Exact proxy addresses, e.g. "10.0.0.4".</summary>
    public string[] KnownProxies { get; set; } = [];

    /// <summary>Proxy address ranges in CIDR notation, e.g. "10.0.0.0/8".</summary>
    public string[] KnownNetworks { get; set; } = [];
}

internal static class ReverseProxy
{
    public static IServiceCollection AddOpsHubReverseProxy(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ReverseProxyOptions>(configuration.GetSection(ReverseProxyOptions.SectionName));

        services.AddOptions<ForwardedHeadersOptions>().Configure<IOptions<ReverseProxyOptions>>((options, proxy) =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

            // Replace the defaults (localhost only) with exactly the configured proxies.
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();
            foreach (var address in proxy.Value.KnownProxies)
            {
                options.KnownProxies.Add(IPAddress.Parse(address));
            }

            foreach (var network in proxy.Value.KnownNetworks)
            {
                options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
            }
        });

        return services;
    }

    /// <summary>Must run first, so everything after it sees the client's IP address and scheme.</summary>
    public static WebApplication UseOpsHubReverseProxy(this WebApplication app)
    {
        var proxy = app.Services.GetRequiredService<IOptions<ReverseProxyOptions>>().Value;
        if (!proxy.Enabled)
        {
            return app;
        }

        if (proxy.KnownProxies.Length == 0 && proxy.KnownNetworks.Length == 0)
        {
            throw new InvalidOperationException(
                "ReverseProxy:Enabled is true, but no ReverseProxy:KnownProxies or ReverseProxy:KnownNetworks are configured.");
        }

        app.UseForwardedHeaders();
        return app;
    }
}
