using System.Text.RegularExpressions;
using Microsoft.AspNetCore.StaticFiles;

namespace OpsHub.Api.Hosting;

/// <summary>
/// In production the API also serves the built Angular app from wwwroot, so the browser
/// talks to one origin: the SameSite=Strict session cookie works and no CORS is needed,
/// exactly like the development proxy. Without a wwwroot/index.html (development, tests)
/// nothing here is switched on.
/// </summary>
internal static partial class Frontend
{
    public static bool IsPresent(IWebHostEnvironment environment) =>
        environment.WebRootFileProvider.GetFileInfo("index.html").Exists;

    /// <summary>The app's JavaScript, styles and fonts: public files, served before sign-in is checked.</summary>
    public static WebApplication UseOpsHubFrontend(this WebApplication app)
    {
        if (IsPresent(app.Environment))
        {
            app.UseStaticFiles(new StaticFileOptions { OnPrepareResponse = SetCacheHeaders });
        }

        return app;
    }

    public static WebApplication MapOpsHubFrontend(this WebApplication app)
    {
        if (!IsPresent(app.Environment))
        {
            return app;
        }

        // An unknown /api URL stays an API error (401 when signed out, 404 when signed in),
        // never the HTML page, which would confuse the Angular error handling.
        app.MapFallback("/api/{**path}", () => Results.NotFound());

        // Any other address without a file extension is a screen of the Angular app, such as
        // /tickets/12 opened from a bookmark: answer with index.html and let the Angular router
        // show the screen. The page holds no data; every API call still requires sign-in.
        app.MapFallbackToFile("{*path:nonfile}", "index.html", new StaticFileOptions { OnPrepareResponse = SetCacheHeaders })
            .AllowAnonymous();

        return app;
    }

    // Angular's production build puts a content hash in file names (main-X6MMBNKY.js). A
    // changed file always gets a new name, so these can be cached for a year. Everything else
    // (index.html above all) is revalidated on each visit, so a new release shows up at once.
    private static void SetCacheHeaders(StaticFileResponseContext context) =>
        context.Context.Response.Headers.CacheControl = HashedFileName().IsMatch(context.File.Name)
            ? "public, max-age=31536000, immutable"
            : "no-cache";

    [GeneratedRegex(@"-[A-Z0-9]{8}\.(js|css|woff2?)$")]
    private static partial Regex HashedFileName();
}
