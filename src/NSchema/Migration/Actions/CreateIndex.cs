using NSchema.Schema;

namespace NSchema.Migration.Actions;

/// <summary>
/// Represents adding a new index to an existing table in the database schema.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the table to which the index will be added.</param>
/// <param name="TableName">The name of the table to which the index will be added.</param>
/// <param name="Index">The definition of the index to be added.</param>
public sealed record CreateIndex(
    string SchemaName,
    string TableName,
    TableIndex Index
) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => false;
}
