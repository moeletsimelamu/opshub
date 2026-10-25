namespace OpsHub.Api.Data.Seeding;

/// <summary>Bound from the "Seed" configuration section.</summary>
public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    public bool Enabled { get; set; }

    /// <summary>
    /// Password given to every fictional demo account. Deliberately not stored in
    /// appsettings: set it with user-secrets or the Seed__DemoPassword environment variable.
    /// </summary>
    public string? DemoPassword { get; set; }
}
