using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using NSchema.Model;
using NSchema.Model.Constraints;
using NSchema.Model.Scripts;

namespace NSchema.Diff.Domain.Constraints;

/// <summary>
/// Describes a change to a table's exclusion constraint. A changed exclusion constraint surfaces as a Remove followed by an Add.
/// </summary>
public sealed record ExclusionConstraintDiff : IMigratableDiff
{
    [JsonConstructor]
    private ExclusionConstraintDiff() { }

    /// <summary>
    /// The change to the exclusion constraint.
    /// </summary>
    public required ChangeKind Kind { get; init; }

    /// <summary>
    /// The exclusion constraint name.
    /// </summary>
    public required SqlIdentifier Name { get; init; }

    /// <summary>
    /// The definition for an added exclusion constraint; otherwise <see langword="null"/>.
    /// </summary>
    public ExclusionConstraint? Definition { get; init; }

    /// <summary>
    /// The change to the comment, if any.
    /// </summary>
    public ValueChange<string>? Comment { get; init; }

    /// <summary>
    /// The change-event script matched to this change, run at this point in the plan (<see langword="null"/> when none).
    /// </summary>
    public ChangeScript? MigrationScript { get; init; }

    /// <summary>
    /// Whether this is a exclusion constraint being created, and so carries the <see cref="Definition"/> to create it from.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Definition))]
    public bool IsAdd() => Kind == ChangeKind.Add && Definition is not null;

    /// <summary>
    /// An exclusion constraint being created, named by its own definition.
    /// </summary>
    public static ExclusionConstraintDiff Added(ExclusionConstraint definition) =>
        new() { Kind = ChangeKind.Add, Name = definition.Name, Definition = definition };

    /// <summary>
    /// An exclusion constraint being dropped.
    /// </summary>
    public static ExclusionConstraintDiff Removed(SqlIdentifier name) =>
        new() { Kind = ChangeKind.Remove, Name = name };

    /// <summary>
    /// A comment change applied in place.
    /// </summary>
    public static ExclusionConstraintDiff CommentChanged(SqlIdentifier name, ValueChange<string> comment) =>
        new() { Kind = ChangeKind.Modify, Name = name, Comment = comment };
}
