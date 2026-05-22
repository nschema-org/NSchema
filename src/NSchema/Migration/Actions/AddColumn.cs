using NSchema.Schema;

namespace NSchema.Migration.Actions;

/// <summary>
/// Represents adding a new column to an existing table in the database schema.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the table to which the column will be added.</param>
/// <param name="TableName">The name of the table to which the column will be added.</param>
/// <param name="Column">The definition of the column to be added.</param>
public sealed record AddColumn(string SchemaName, string TableName, Column Column) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => false;
}
