namespace OpsHub.Api.Auth;

/// <summary>Names of the authorisation policies used in [Authorize(Policy = ...)].</summary>
public static class AuthPolicies
{
    /// <summary>Support Agents and Administrators.</summary>
    public const string StaffOnly = "StaffOnly";

    public const string AdminOnly = "AdminOnly";
}

public static class RateLimitPolicies
{
    public const string Login = "login";
}
