namespace NSchema.Migration.Actions;

/// <summary>
/// Represents the removal of an existing index from a table in the database schema.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the table from which the index will be removed.</param>
/// <param name="TableName">The name of the table from which the index will be removed.</param>
/// <param name="IndexName">The name of the index to be removed.</param>
public sealed record DropIndex(
    string SchemaName,
    string TableName,
    string IndexName
) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => false;
}
