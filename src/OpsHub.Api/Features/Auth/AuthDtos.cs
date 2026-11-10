using System.ComponentModel.DataAnnotations;

namespace OpsHub.Api.Features.Auth;

// Request DTOs use init properties rather than positional records so that validation
// errors are keyed by the JSON field name ("email"), which the Angular forms rely on.

public sealed record LoginRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; init; } = string.Empty;

    [Required, MaxLength(128)]
    public string Password { get; init; } = string.Empty;
}

public sealed record ChangePasswordRequest
{
    [Required, MaxLength(128)]
    public string CurrentPassword { get; init; } = string.Empty;

    [Required, MaxLength(128)]
    public string NewPassword { get; init; } = string.Empty;
}

public sealed record ForgotPasswordRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; init; } = string.Empty;
}

public sealed record ResetPasswordRequest
{
    /// <summary>From the reset link. A user ID rather than the email, so the link holds no personal data.</summary>
    [Required]
    public Guid? UserId { get; init; }

    [Required, MaxLength(2048)]
    public string Token { get; init; } = string.Empty;

    [Required, MaxLength(128)]
    public string NewPassword { get; init; } = string.Empty;
}

/// <summary>What the sign-in page may offer. PasswordReset is true only when email is set up.</summary>
public sealed record AuthOptionsResponse(bool PasswordReset);

/// <summary>Who is signed in. The UI uses Role to decide what to show; the API re-checks every request.</summary>
public sealed record CurrentUserResponse(Guid Id, string Email, string DisplayName, string Role);
