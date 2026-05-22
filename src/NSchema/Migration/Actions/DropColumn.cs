namespace NSchema.Migration.Actions;

/// <summary>
/// Represents the removal of an existing column from a table in the database schema.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the table from which the column will be removed.</param>
/// <param name="TableName">The name of the table from which the column will be removed.</param>
/// <param name="ColumnName">The name of the column to be removed.</param>
public sealed record DropColumn(string SchemaName, string TableName, string ColumnName) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => true;
}
