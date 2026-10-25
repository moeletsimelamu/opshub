namespace OpsHub.Api.Security;

/// <summary>
/// Defensive HTTP response headers. API responses are JSON, so they get the strictest
/// policy: nothing may be loaded, framed or sniffed. The Angular app's HTML page gets a
/// policy that allows only the app's own files. The interactive API docs (/scalar,
/// Development only) load their own scripts and styles, so they are exempt.
/// </summary>
internal static class SecurityHeaders
{
    private const string ApiPolicy = "default-src 'none'; frame-ancestors 'none'; base-uri 'none'";

    // Scripts only from this origin: no inline scripts, no eval, no third-party code, so an
    // injected <script> can't run. Styles allow 'unsafe-inline' because Angular adds each
    // component's styles as <style> elements at runtime; injected CSS can change how a page
    // looks but can't run code. data: images are the SVG icons inside the stylesheet.
    private const string AppPolicy =
        "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; " +
        "font-src 'self'; connect-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;

                // Don't guess content types: a JSON response must never be run as a script.
                headers["X-Content-Type-Options"] = "nosniff";

                // Don't leak URLs (which contain ticket numbers) to other sites.
                headers["Referrer-Policy"] = "no-referrer";

                // No page from this origin may be framed (clickjacking).
                headers["X-Frame-Options"] = "DENY";

                // No browser features (camera, location…) are needed.
                headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";

                if (!context.Request.Path.StartsWithSegments("/scalar"))
                {
                    var isPage = context.Response.ContentType?.StartsWith("text/html", StringComparison.OrdinalIgnoreCase) == true;
                    headers["Content-Security-Policy"] = isPage ? AppPolicy : ApiPolicy;
                }

                return Task.CompletedTask;
            });

            await next();
        });
}
