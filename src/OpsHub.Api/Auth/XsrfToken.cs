using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;

namespace OpsHub.Api.Auth;

/// <summary>
/// Cross-site request forgery (CSRF) protection using the "double submit" pattern that
/// Angular supports out of the box:
/// 1. The API puts a token in a cookie JavaScript can read (XSRF-TOKEN).
/// 2. Angular copies it into the X-XSRF-TOKEN header on POST/PUT/PATCH/DELETE.
/// 3. The API rejects state-changing requests whose header does not match.
/// Another site can make the browser send cookies, but it cannot read them to set the header.
/// </summary>
internal static class XsrfToken
{
    public const string CookieName = "XSRF-TOKEN";
    public const string HeaderName = "X-XSRF-TOKEN";

    /// <summary>
    /// Issues a token bound to <paramref name="principal"/>. Tokens belong to a specific
    /// user, so a new one is issued whenever the user signs in or out.
    /// </summary>
    public static void Issue(HttpContext httpContext, IAntiforgery antiforgery, ClaimsPrincipal principal)
    {
        httpContext.User = principal;
        var tokens = antiforgery.GetAndStoreTokens(httpContext);

        httpContext.Response.Cookies.Append(CookieName, tokens.RequestToken!, new CookieOptions
        {
            HttpOnly = false, // Angular must be able to read this one.
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/",
        });
    }
}
