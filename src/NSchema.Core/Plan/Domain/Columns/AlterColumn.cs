using NSchema.Diff.Domain;
using NSchema.Model;
using NSchema.Model.Columns;

namespace NSchema.Plan.Domain.Columns;

/// <summary>
/// Represents changing an existing column's type or nullability.
/// </summary>
/// <param name="Table">The address of the column's table.</param>
/// <param name="Column">The column's final definition.</param>
/// <param name="Type">The type change, if any.</param>
/// <param name="Nullability">The nullability change, if any.</param>
public sealed record AlterColumn(
    ObjectAddress Table,
    Column Column,
    ValueChange<SqlType>? Type = null,
    ValueChange<bool>? Nullability = null
) : MigrationAction;
