namespace OpsHub.Api.Data;

/// <summary>Bound from the "Database" configuration section.</summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>
    /// Convenient for local development. In production, apply migrations as a
    /// deliberate deployment step instead.
    /// </summary>
    public bool ApplyMigrationsOnStartup { get; set; }
}
