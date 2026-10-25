namespace OpsHub.Api.Auth;

/// <summary>Bound from the "Auth" configuration section.</summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>
    /// Login requests allowed per client IP address per minute. Slows down password
    /// guessing across many accounts; per-account lockout handles guessing on one account.
    /// </summary>
    public int LoginAttemptsPerMinute { get; set; } = 10;
}
