using NSchema.Schema;

namespace NSchema.Migration.Actions;

/// <summary>
/// Represents adding a new primary key constraint to an existing table in the database schema.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the table to which the primary key will be added.</param>
/// <param name="TableName">The name of the table to which the primary key will be added.</param>
/// <param name="PrimaryKey">The definition of the primary key constraint to be added.</param>
public sealed record AddPrimaryKey(
    string SchemaName,
    string TableName,
    PrimaryKey PrimaryKey
) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => false;
}
