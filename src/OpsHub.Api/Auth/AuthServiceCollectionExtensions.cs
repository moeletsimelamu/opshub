using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using OpsHub.Api.Data;
using OpsHub.Domain.Users;

namespace OpsHub.Api.Auth;

public static class AuthServiceCollectionExtensions
{
    /// <summary>
    /// Registers user storage, password hashing, the session cookie, authorisation
    /// policies, CSRF protection and login rate limiting.
    /// </summary>
    public static IServiceCollection AddOpsHubAuth(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AuthOptions>(configuration.GetSection(AuthOptions.SectionName));

        // ---------- Identity: users, roles, password hashing, lockout ----------
        services
            .AddIdentityCore<AppUser>(options =>
            {
                options.User.RequireUniqueEmail = true;

                // Follows NIST SP 800-63B: require length, not character-class rules.
                options.Password.RequiredLength = 12;
                options.Password.RequiredUniqueChars = 4;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;

                // 5 wrong passwords lock the account for 15 minutes.
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<OpsHubDbContext>()
            .AddSignInManager<OpsHubSignInManager>();

        // SignInManager is resolved as OpsHubSignInManager too, so controllers can use our additions.
        services.AddScoped(sp => (OpsHubSignInManager)sp.GetRequiredService<SignInManager<AppUser>>());

        // ---------- Session cookie ----------
        services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
        services.ConfigureApplicationCookie(options =>
        {
            // "__Host-" makes browsers require Secure, Path=/ and no Domain, so the cookie
            // cannot be set or overwritten by another subdomain.
            options.Cookie.Name = "__Host-opshub-session";
            options.Cookie.HttpOnly = true;                          // JavaScript cannot read it (XSS can't steal it).
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // Only sent over HTTPS.
            options.Cookie.SameSite = SameSiteMode.Strict;           // Never sent on requests started by other sites.
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = true;

            // Cookie auth is built for server-rendered sites and redirects to a login page.
            // An API must answer with a status code instead; UseStatusCodePages adds the
            // ProblemDetails body.
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });

        // Every minute, each session is checked against the database. Changing a user's
        // security stamp (deactivation, role change, password change) ends their other sessions.
        services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.FromMinutes(1));

        // ---------- Authorisation ----------
        services.AddAuthorizationBuilder()
            // Secure by default: an endpoint without [AllowAnonymous] requires a signed-in user,
            // even if someone forgets [Authorize].
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(AuthPolicies.StaffOnly, policy => policy.RequireRole(AppRoles.SupportAgent, AppRoles.Administrator))
            .AddPolicy(AuthPolicies.AdminOnly, policy => policy.RequireRole(AppRoles.Administrator));

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

        // ---------- CSRF protection ----------
        services.AddAntiforgery(options =>
        {
            options.HeaderName = XsrfToken.HeaderName;
            options.Cookie.Name = "__Host-opshub-antiforgery";
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
        });

        // ---------- Login rate limiting ----------
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(RateLimitPolicies.Login, httpContext =>
            {
                var limit = httpContext.RequestServices.GetRequiredService<IOptions<AuthOptions>>().Value.LoginAttemptsPerMinute;
                var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                return RateLimitPartition.GetFixedWindowLimiter(clientIp, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = limit,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                });
            });
        });

        return services;
    }
}
