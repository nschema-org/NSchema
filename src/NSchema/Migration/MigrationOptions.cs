namespace NSchema.Migration;

/// <summary>
/// Represents options that can be configured for a migration operation.
/// </summary>
public class MigrationOptions
{
    /// <summary>
    /// Specifies the policy to apply when a destructive action is encountered during the migration process.
    /// </summary>
    /// <remarks>
    /// A destructive action is an operation that could potentially lead to data loss, such as dropping a table or column.
    /// The policy determines how the migrator should handle such actions, whether to allow them, block them, or raise an error.
    /// </remarks>
    public DestructiveActionPolicy DestructiveActionPolicy { get; set; } = DestructiveActionPolicy.Error;

    /// <summary>
    /// Indicates whether the migration should be executed in "dry run" mode, where the generated SQL commands are not
    /// actually executed against the database, but instead are logged or returned for review.
    /// </summary>
    public bool DryRun { get; set; } = false;
}
