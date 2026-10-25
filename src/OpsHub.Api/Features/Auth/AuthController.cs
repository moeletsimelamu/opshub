using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OpsHub.Api.Auth;
using OpsHub.Api.Common;
using OpsHub.Domain.Users;

namespace OpsHub.Api.Features.Auth;

[Route("api/auth")]
public sealed partial class AuthController(
    UserManager<AppUser> userManager,
    OpsHubSignInManager signInManager,
    IAntiforgery antiforgery,
    ILogger<AuthController> logger) : ApiControllerBase
{
    /// <summary>
    /// Issues the XSRF-TOKEN cookie. The Angular app calls this once at startup so that
    /// its first POST (the login form) carries a valid token.
    /// </summary>
    [HttpGet("antiforgery")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public NoContentResult GetAntiforgeryToken()
    {
        XsrfToken.Issue(HttpContext, antiforgery, User);
        return NoContent();
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<CurrentUserResponse>> Login(LoginRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            signInManager.SimulatePasswordCheck(request.Password);
            LogSignInFailed(logger, "unknown email", null);
            return SignInFailed();
        }

        // isPersistent: false gives a session cookie, removed when the browser closes.
        // lockoutOnFailure: true counts wrong passwords towards the lockout limit.
        var result = await signInManager.PasswordSignInAsync(user, request.Password, isPersistent: false, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            var reason = result.IsLockedOut ? "locked out" : result.IsNotAllowed ? "account inactive" : "wrong password";
            LogSignInFailed(logger, reason, user.Id);

            // Same response for every failure, so the API never reveals whether an email
            // has an account, is locked or is deactivated.
            return SignInFailed();
        }

        LogSignedIn(logger, user.Id);

        var principal = await signInManager.CreateUserPrincipalAsync(user);
        XsrfToken.Issue(HttpContext, antiforgery, principal);
        return Ok(await ToResponseAsync(user));
    }

    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<NoContentResult> Logout()
    {
        await signInManager.SignOutAsync();
        XsrfToken.Issue(HttpContext, antiforgery, new ClaimsPrincipal(new ClaimsIdentity()));
        return NoContent();
    }

    [HttpGet("me")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CurrentUserResponse>> Me()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Unauthorized();
        }

        XsrfToken.Issue(HttpContext, antiforgery, User);
        return Ok(await ToResponseAsync(user));
    }

    [HttpPost("change-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> ChangePassword(ChangePasswordRequest request)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Unauthorized();
        }

        var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                var field = error.Code == nameof(IdentityErrorDescriber.PasswordMismatch) ? "currentPassword" : "newPassword";
                var message = field == "currentPassword" ? "Current password is incorrect." : error.Description;
                ModelState.AddModelError(field, message);
            }

            return ValidationProblem(ModelState);
        }

        // Changing the password changed the security stamp, which ends every other
        // session. Re-issue this session's cookie so the current browser stays signed in.
        await signInManager.RefreshSignInAsync(user);
        return NoContent();
    }

    private ObjectResult SignInFailed() =>
        Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            title: "Sign-in failed",
            detail: "The email or password is incorrect, or the account is unavailable. " +
                    "After 5 failed attempts an account is locked for 15 minutes.");

    private async Task<CurrentUserResponse> ToResponseAsync(AppUser user)
    {
        var roles = await userManager.GetRolesAsync(user);
        return new CurrentUserResponse(user.Id, user.Email!, user.DisplayName, roles.FirstOrDefault() ?? string.Empty);
    }

    // Log user IDs, never passwords, and not the attempted email (it may be a typo of
    // someone else's address or a password pasted into the wrong field).
    [LoggerMessage(Level = LogLevel.Warning, Message = "Sign-in failed ({Reason}) for user {UserId}")]
    private static partial void LogSignInFailed(ILogger logger, string reason, Guid? userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "User {UserId} signed in")]
    private static partial void LogSignedIn(ILogger logger, Guid userId);
}
