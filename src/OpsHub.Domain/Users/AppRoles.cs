namespace OpsHub.Domain.Users;

/// <summary>
/// Role names stored in the AspNetRoles table. Use these constants instead of
/// string literals so a typo becomes a compile error.
/// </summary>
public static class AppRoles
{
    public const string Administrator = "Administrator";
    public const string SupportAgent = "SupportAgent";
    public const string Requester = "Requester";

    public static readonly IReadOnlyList<string> All = [Administrator, SupportAgent, Requester];
}
