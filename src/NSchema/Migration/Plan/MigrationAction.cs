namespace NSchema.Migration.Plan;

/// <summary>
/// Represents a single action to be performed during a database migration.
/// </summary>
public abstract record MigrationAction
{
    /// <summary>
    /// Indicates whether this migration action may result in data loss or irreversible changes to the database schema.
    /// </summary>
    public abstract bool IsDestructive { get; }
}
