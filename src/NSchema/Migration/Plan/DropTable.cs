namespace NSchema.Migration.Plan;

/// <summary>
/// Represents the removal of an existing table from the database schema.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the table to be removed.</param>
/// <param name="TableName">The name of the table to be removed.</param>
public sealed record DropTable(string SchemaName, string TableName) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => true;
}
