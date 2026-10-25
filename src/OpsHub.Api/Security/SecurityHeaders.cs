namespace OpsHub.Api.Security;

/// <summary>
/// Defensive HTTP response headers. The API returns JSON, so it can use the strictest
/// policy: nothing may be loaded, framed or sniffed. The interactive API docs (/scalar,
/// Development only) need scripts and styles, so they are exempt.
/// The Angular app gets its own policy when the API starts serving it (Phase 10).
/// </summary>
internal static class SecurityHeaders
{
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
                    headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'; base-uri 'none'";
                }

                return Task.CompletedTask;
            });

            await next();
        });
}
