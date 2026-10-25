namespace OpsHub.Api.Data.Seeding;

/// <summary>
/// Bound from the "Bootstrap" configuration section. Creates the first administrator of a
/// new installation, so someone can sign in and add the other users through the app.
/// </summary>
public sealed class BootstrapOptions
{
    public const string SectionName = "Bootstrap";

    public string? AdminEmail { get; set; }

    public string AdminDisplayName { get; set; } = "Administrator";

    /// <summary>
    /// Never put this in appsettings: pass it as the Bootstrap__AdminPassword environment
    /// variable (or a platform secret) for the first start, then remove it.
    /// </summary>
    public string? AdminPassword { get; set; }
}
