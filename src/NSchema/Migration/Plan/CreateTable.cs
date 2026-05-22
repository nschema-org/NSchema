using NSchema.Schema;

namespace NSchema.Migration.Plan;

/// <summary>
/// Represents the creation of a new table in the database schema.
/// </summary>
/// <param name="SchemaName">The name of the schema in which the new table will be created.</param>
/// <param name="Table">The definition of the new table to be created, including its name, columns, and constraints.</param>
public sealed record CreateTable(string SchemaName, Table Table) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => false;
}
