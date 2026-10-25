namespace OpsHub.Api.Security;

/// <summary>
/// The session and XSRF cookies are Secure-only, so the API can't work over plain HTTP.
/// Without this check the first sign-in step fails with a 500 from the antiforgery system,
/// which hides the real cause. That cause is usually a proxy that handles HTTPS but isn't
/// listed under ReverseProxy, so the app believes every request arrived over HTTP.
/// /health and the Angular page stay reachable over HTTP: platform health probes use it.
/// </summary>
internal static partial class HttpsOnlyApi
{
    public static WebApplication UseHttpsOnlyApi(this WebApplication app)
    {
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(HttpsOnlyApi).FullName!);

        app.Use(async (context, next) =>
        {
            if (!context.Request.IsHttps && context.Request.Path.StartsWithSegments("/api"))
            {
                LogPlainHttp(logger, context.Request.Path);
                await Results.Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "HTTPS is required",
                        detail: "This API only accepts requests made over HTTPS.")
                    .ExecuteAsync(context);
                return;
            }

            await next();
        });

        return app;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Refused an API request made over plain HTTP ({Path}). If HTTPS is handled " +
        "by a reverse proxy, set ReverseProxy:Enabled and its KnownProxies or KnownNetworks")]
    private static partial void LogPlainHttp(ILogger logger, string path);
}
