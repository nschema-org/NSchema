using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using NSchema.Model;
using NSchema.Model.Scripts;
using NSchema.Model.Tables;

namespace NSchema.Diff.Domain.Constraints;

/// <summary>
/// Describes a change to a table's foreign key. A changed foreign key surfaces as a Remove followed by an Add.
/// </summary>
public sealed record ForeignKeyDiff : IMigratableDiff
{
    [JsonConstructor]
    private ForeignKeyDiff() { }

    /// <summary>
    /// The change to the foreign key.
    /// </summary>
    public required ChangeKind Kind { get; init; }

    /// <summary>
    /// The foreign key name.
    /// </summary>
    public required SqlIdentifier Name { get; init; }

    /// <summary>
    /// The definition for an added foreign key; otherwise <see langword="null"/>.
    /// </summary>
    public ForeignKey? Definition { get; init; }

    /// <summary>
    /// The change to the comment, if any.
    /// </summary>
    public ValueChange<string>? Comment { get; init; }

    /// <summary>
    /// The change-event script matched to this change, run at this point in the plan (<see langword="null"/> when none).
    /// </summary>
    public ChangeScript? MigrationScript { get; init; }

    /// <summary>
    /// Whether this is a foreign key being created, and so carries the <see cref="Definition"/> to create it from.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Definition))]
    public bool IsAdd() => Kind == ChangeKind.Add && Definition is not null;

    /// <summary>
    /// A foreign key being created, named by its own definition.
    /// </summary>
    public static ForeignKeyDiff Added(ForeignKey definition) =>
        new() { Kind = ChangeKind.Add, Name = definition.Name, Definition = definition };

    /// <summary>
    /// A foreign key being dropped.
    /// </summary>
    public static ForeignKeyDiff Removed(SqlIdentifier name) =>
        new() { Kind = ChangeKind.Remove, Name = name };

    /// <summary>
    /// A comment change applied in place.
    /// </summary>
    public static ForeignKeyDiff CommentChanged(SqlIdentifier name, ValueChange<string> comment) =>
        new() { Kind = ChangeKind.Modify, Name = name, Comment = comment };
}
