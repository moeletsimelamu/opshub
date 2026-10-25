using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using OpsHub.Domain.Users;

namespace OpsHub.Api.Auth;

/// <summary>
/// Identity's SignInManager with two OpsHub additions: deactivated accounts cannot
/// sign in, and unknown emails cost the same time as real ones.
/// </summary>
public sealed class OpsHubSignInManager(
    UserManager<AppUser> userManager,
    IHttpContextAccessor contextAccessor,
    IUserClaimsPrincipalFactory<AppUser> claimsFactory,
    IOptions<IdentityOptions> optionsAccessor,
    ILogger<SignInManager<AppUser>> logger,
    IAuthenticationSchemeProvider schemes,
    IUserConfirmation<AppUser> confirmation,
    IPasswordHasher<AppUser> passwordHasher)
    : SignInManager<AppUser>(userManager, contextAccessor, claimsFactory, optionsAccessor, logger, schemes, confirmation)
{
    // A real hash of a random value, computed once. Verifying against it takes as long
    // as verifying a real user's password.
    private readonly Lazy<string> _dummyHash = new(() => passwordHasher.HashPassword(new AppUser(), Guid.NewGuid().ToString()));

    /// <summary>PasswordSignInAsync calls this first; returning false yields SignInResult.NotAllowed.</summary>
    public override async Task<bool> CanSignInAsync(AppUser user) =>
        user.IsActive && await base.CanSignInAsync(user);

    /// <summary>
    /// Hashing a password deliberately takes tens of milliseconds. If unknown emails
    /// returned instantly, an attacker could time responses to discover which emails
    /// have accounts. Doing a throwaway verification removes that difference.
    /// </summary>
    public void SimulatePasswordCheck(string password) =>
        passwordHasher.VerifyHashedPassword(new AppUser(), _dummyHash.Value, password);
}
