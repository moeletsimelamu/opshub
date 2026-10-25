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

/// <summary>Who is signed in. The UI uses Role to decide what to show; the API re-checks every request.</summary>
public sealed record CurrentUserResponse(Guid Id, string Email, string DisplayName, string Role);
