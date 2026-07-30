using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using NSchema.Model;
using NSchema.Model.Indexes;

namespace NSchema.Diff.Domain.Indexes;

/// <summary>
/// Describes a change to a table index.
/// </summary>
public sealed record IndexDiff : IObjectMemberDiff
{
    [JsonConstructor]
    private IndexDiff() { }

    /// <summary>
    /// The change to the index.
    /// </summary>
    public required ChangeKind Change { get; init; }

    /// <summary>
    /// The index name.
    /// </summary>
    public required SqlIdentifier Name { get; init; }

    /// <summary>
    /// The definition for an added index; otherwise <see langword="null"/>.
    /// </summary>
    public TableIndex? Definition { get; init; }

    /// <summary>
    /// The change to the comment, if any.
    /// </summary>
    public ValueChange<string>? Comment { get; init; }

    /// <summary>
    /// Whether this is a index being created, and so carries the <see cref="Definition"/> to create it from.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Definition))]
    public bool IsAdd() => Change == ChangeKind.Add && Definition is not null;

    /// <summary>
    /// An index being created, named by its own definition.
    /// </summary>
    public static IndexDiff Added(TableIndex definition) =>
        new() { Change = ChangeKind.Add, Name = definition.Name, Definition = definition };

    /// <summary>
    /// An index being dropped.
    /// </summary>
    public static IndexDiff Removed(SqlIdentifier name) =>
        new() { Change = ChangeKind.Remove, Name = name };

    /// <summary>
    /// A comment change applied in place.
    /// </summary>
    public static IndexDiff CommentChanged(SqlIdentifier name, ValueChange<string> comment) =>
        new() { Change = ChangeKind.Modify, Name = name, Comment = comment };
}
