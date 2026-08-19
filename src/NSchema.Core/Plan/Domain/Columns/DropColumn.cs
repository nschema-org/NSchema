using NSchema.Model;
using NSchema.Model.Columns;

namespace NSchema.Plan.Domain.Columns;

/// <summary>
/// Represents the removal of an existing column from a table in the database schema.
/// </summary>
/// <param name="Table">The address of the table.</param>
/// <param name="Column">The column to be removed, including its type and nullability.</param>
public sealed record DropColumn(ObjectAddress Table, Column Column) : MigrationAction
{
    /// <summary>The name of the column to be removed.</summary>
    public SqlIdentifier ColumnName => Column.Name;
}
