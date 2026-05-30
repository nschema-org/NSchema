namespace NSchema.Policies;

/// <summary>
/// The severity of a <see cref="PolicyError"/> finding.
/// </summary>
public enum PolicySeverity
{
    /// <summary>
    /// Informational; no action required.
    /// </summary>
    Info,

    /// <summary>
    /// The finding warrants attention but does not block the migration.
    /// </summary>
    Warning,

    /// <summary>
    /// The finding blocks the migration.
    /// </summary>
    Error
}
