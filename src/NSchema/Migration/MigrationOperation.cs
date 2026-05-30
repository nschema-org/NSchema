namespace NSchema.Migration;

/// <summary>
/// The operation to perform when running the migration.
/// </summary>
public enum MigrationOperation
{
    /// <summary>
    /// Compute and render the migration plan without applying it to the target.
    /// </summary>
    Plan,

    /// <summary>
    /// Compute the plan and apply it to the target.
    /// </summary>
    Apply,

    /// <summary>
    /// Read the live current schema and write it to the state store, without planning or applying anything.
    /// </summary>
    Refresh,
}
