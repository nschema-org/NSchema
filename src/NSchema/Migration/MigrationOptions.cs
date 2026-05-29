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
    /// The operation the migration run performs. Defaults to <see cref="MigrationOperation.Apply"/>,
    /// which preserves the historical <c>DryRun = false</c> behaviour.
    /// </summary>
    public MigrationOperation Operation { get; set; } = MigrationOperation.Apply;

    /// <summary>
    /// Indicates whether the migration should be executed in "dry run" mode, where the generated SQL commands are not
    /// actually executed against the database, but instead are logged or returned for review.
    /// </summary>
    /// <remarks>
    /// This is a projection over <see cref="Operation"/>: it reads <see langword="true"/> when the
    /// operation is <see cref="MigrationOperation.Plan"/>, and setting it maps to
    /// <see cref="MigrationOperation.Plan"/> / <see cref="MigrationOperation.Apply"/>.
    /// </remarks>
    [Obsolete("Use Operation instead. DryRun will be removed in a future major version.")]
    public bool DryRun
    {
        get => Operation == MigrationOperation.Plan;
        set => Operation = value ? MigrationOperation.Plan : MigrationOperation.Apply;
    }

    /// <summary>
    /// Controls whether the executor wraps the migration in a transaction.
    /// </summary>
    public TransactionMode TransactionMode { get; set; } = TransactionMode.Single;

    /// <summary>
    /// The set of schema names the migration is scoped to.
    /// </summary>
    /// <remarks>
    /// When non-empty, only these schemas are read from the database, validated, and diffed;
    /// declared or dropped schemas outside this set are ignored.
    /// </remarks>
    public string[]? SchemaNames { get; set; }
}
