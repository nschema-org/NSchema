namespace NSchema.Migration.Actions;

/// <summary>
/// Represents the removal of an existing primary key constraint from a table in the database schema.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the table from which the primary key will be removed.</param>
/// <param name="TableName">The name of the table from which the primary key will be removed.</param>
/// <param name="PrimaryKeyName">The name of the primary key constraint to be removed.</param>
public sealed record DropPrimaryKey(
    string SchemaName,
    string TableName,
    string PrimaryKeyName
) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => false;
}
