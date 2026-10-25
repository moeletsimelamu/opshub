namespace OpsHub.Api.Data;

/// <summary>Bound from the "Database" configuration section.</summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>
    /// Off by default. Safe when exactly one instance runs, which SQLite requires anyway (the
    /// container image turns it on). With several instances, apply migrations as a separate
    /// deployment step instead, so two instances never migrate at once (docs/DEPLOYMENT.md).
    /// </summary>
    public bool ApplyMigrationsOnStartup { get; set; }
}
