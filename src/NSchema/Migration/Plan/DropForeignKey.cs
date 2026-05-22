namespace NSchema.Migration.Plan;

/// <summary>
/// Represents the removal of an existing foreign key constraint from a table in the database schema.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the table from which the foreign key will be removed.</param>
/// <param name="TableName">The name of the table from which the foreign key will be removed.</param>
/// <param name="ForeignKeyName">The name of the foreign key constraint to be removed.</param>
public sealed record DropForeignKey(
    string SchemaName,
    string TableName,
    string ForeignKeyName
) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => false;
}
