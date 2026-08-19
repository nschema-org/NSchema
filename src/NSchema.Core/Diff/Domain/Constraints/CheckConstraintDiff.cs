using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using NSchema.Model;
using NSchema.Model.Constraints;
using NSchema.Model.Scripts;

namespace NSchema.Diff.Domain.Constraints;

/// <summary>
/// Describes a change to a table's check constraint. A changed check constraint surfaces as a Remove followed by an Add.
/// </summary>
public sealed record CheckConstraintDiff : IMigratableDiff
{
    [JsonConstructor]
    private CheckConstraintDiff() { }

    /// <summary>
    /// The change to the check constraint.
    /// </summary>
    public required ChangeKind Change { get; init; }

    /// <summary>
    /// The check constraint name.
    /// </summary>
    public required SqlIdentifier Name { get; init; }

    /// <summary>
    /// The definition for an added check constraint; otherwise <see langword="null"/>.
    /// </summary>
    public CheckConstraint? Definition { get; init; }

    /// <summary>
    /// The change to the comment, if any.
    /// </summary>
    public ValueChange<string>? Comment { get; init; }

    /// <summary>
    /// The change-event script matched to this change, run at this point in the plan (<see langword="null"/> when none).
    /// </summary>
    public ChangeScript? MigrationScript { get; init; }

    /// <summary>
    /// Whether this is a check constraint being created, and so carries the <see cref="Definition"/> to create it from.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Definition))]
    public bool IsAdd() => Change == ChangeKind.Add && Definition is not null;

    /// <summary>
    /// A check constraint being created, named by its own definition.
    /// </summary>
    public static CheckConstraintDiff Added(CheckConstraint definition) =>
        new() { Change = ChangeKind.Add, Name = definition.Name, Definition = definition };

    /// <summary>
    /// A check constraint being dropped.
    /// </summary>
    public static CheckConstraintDiff Removed(SqlIdentifier name) =>
        new() { Change = ChangeKind.Remove, Name = name };

    /// <summary>
    /// A comment change applied in place.
    /// </summary>
    public static CheckConstraintDiff CommentChanged(SqlIdentifier name, ValueChange<string> comment) =>
        new() { Change = ChangeKind.Modify, Name = name, Comment = comment };
}
