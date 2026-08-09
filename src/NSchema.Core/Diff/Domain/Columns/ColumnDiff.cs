using System.Text.Json.Serialization;
using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Scripts;

namespace NSchema.Diff.Domain.Columns;

/// <summary>
/// Describes the changes affecting a single column.
/// </summary>
public sealed record ColumnDiff : IMigratableDiff
{
    [JsonConstructor]
    private ColumnDiff() { }

    /// <summary>
    /// The column name (the new name when renamed).
    /// </summary>
    public required SqlIdentifier Name { get; init; }

    /// <summary>
    /// The change to the column.
    /// </summary>
    public required ChangeKind Change { get; init; }

    /// <summary>
    /// The full column definition: the declared column, or the recorded one when it is being dropped.
    /// </summary>
    public required Column Definition { get; init; }

    /// <summary>
    /// The previous column name when renamed; otherwise <see langword="null"/>.
    /// </summary>
    public SqlIdentifier? RenamedFrom { get; init; }

    /// <summary>
    /// The change to the column's type, if any.
    /// </summary>
    public ValueChange<SqlType>? Type { get; init; }

    /// <summary>
    /// The change to the column's nullability, if any.
    /// </summary>
    public ValueChange<bool>? Nullability { get; init; }

    /// <summary>
    /// The change to the column's default value, if any.
    /// </summary>
    public ValueChange<SqlDefaultExpression>? Default { get; init; }

    /// <summary>
    /// The change to the column's identity options, if any.
    /// </summary>
    public ValueChange<IdentityOptions>? Identity { get; init; }

    /// <summary>
    /// The change to the column's comment, if any.
    /// </summary>
    public ValueChange<string>? Comment { get; init; }

    /// <summary>
    /// The change to the column's stored generation expression, if any.
    /// </summary>
    public ValueChange<SqlText>? Generated { get; init; }

    /// <summary>
    /// The change to whether this column is the table's row identifier for merge replication.
    /// </summary>
    public ValueChange<bool>? RowGuid { get; init; }

    /// <summary>
    /// The change-event script matched to this change, run at this point in the plan (<see langword="null"/> when none).
    /// </summary>
    public ChangeScript? MigrationScript { get; init; }

    /// <summary>
    /// A column being added, carrying its declared comment as a change onto nothing.
    /// </summary>
    public static ColumnDiff Added(Column definition) => new()
    {
        Name = definition.Name,
        Change = ChangeKind.Add,
        Definition = definition,
        Comment = definition.Comment is null ? null : new ValueChange<string>(null, definition.Comment),
    };

    /// <summary>
    /// A column being dropped, carrying the recorded definition it is dropped from.
    /// </summary>
    public static ColumnDiff Removed(Column definition) =>
        new() { Name = definition.Name, Change = ChangeKind.Remove, Definition = definition };

    /// <summary>
    /// A column altered in place; the individual changes are set on the result.
    /// </summary>
    public static ColumnDiff Modified(Column definition) =>
        new() { Name = definition.Name, Change = ChangeKind.Modify, Definition = definition };
}
