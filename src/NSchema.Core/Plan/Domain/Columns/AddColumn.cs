using NSchema.Model;
using NSchema.Model.Columns;

namespace NSchema.Plan.Domain.Columns;

/// <summary>
/// Represents adding a new column to an existing table in the database schema.
/// </summary>
/// <param name="Table">The address of the table.</param>
/// <param name="Column">The definition of the column to be added.</param>
public sealed record AddColumn(ObjectAddress Table, Column Column) : MigrationAction;
