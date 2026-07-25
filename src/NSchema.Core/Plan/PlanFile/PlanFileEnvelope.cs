using NSchema.Plan.Domain;

namespace NSchema.Plan.PlanFile;

/// <summary>
/// The envelope for persisting a saved plan file.
/// </summary>
/// <param name="Plan">The reviewed plan: its diff, its scripts, and the SQL to execute it.</param>
/// <param name="CreatedAt">When the plan was created.</param>
public sealed record PlanFileEnvelope(
    MigrationPlan Plan,
    DateTimeOffset CreatedAt
)
{
    /// <summary>
    /// The current envelope format version.
    /// </summary>
    public const int CurrentVersion = 1;

    /// <summary>
    /// The format version of the payload. Owned by the format, not the caller: it defaults to
    /// <see cref="CurrentVersion"/> on write and is validated by the writer on read.
    /// </summary>
    public int Version { get; init; } = CurrentVersion;
}
