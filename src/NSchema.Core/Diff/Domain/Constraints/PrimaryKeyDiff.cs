using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using NSchema.Model;
using NSchema.Model.Scripts;
using NSchema.Model.Tables;

namespace NSchema.Diff.Domain.Constraints;

/// <summary>
/// Describes a change to a table's primary key.
/// </summary>
public sealed record PrimaryKeyDiff : IMigratableDiff
{
    [JsonConstructor]
    private PrimaryKeyDiff() { }

    /// <summary>
    /// The change to the primary key.
    /// </summary>
    public required ChangeKind Kind { get; init; }

    /// <summary>
    /// The primary key constraint name.
    /// </summary>
    public required SqlIdentifier Name { get; init; }

    /// <summary>
    /// The primary key definition for an added primary key; otherwise <see langword="null"/>.
    /// </summary>
    public PrimaryKey? Definition { get; init; }

    /// <summary>
    /// The change to the constraint's comment, if any.
    /// </summary>
    public ValueChange<string>? Comment { get; init; }

    /// <summary>
    /// The change-event script matched to this change, run at this point in the plan (<see langword="null"/> when none).
    /// </summary>
    public ChangeScript? MigrationScript { get; init; }

    /// <summary>
    /// Whether this is a primary key being created, and so carries the <see cref="Definition"/> to create it from.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Definition))]
    public bool IsAdd() => Kind == ChangeKind.Add && Definition is not null;

    /// <summary>
    /// A primary key being created, named by its own definition.
    /// </summary>
    public static PrimaryKeyDiff Added(PrimaryKey definition) =>
        new() { Kind = ChangeKind.Add, Name = definition.Name, Definition = definition };

    /// <summary>
    /// A primary key being dropped.
    /// </summary>
    public static PrimaryKeyDiff Removed(SqlIdentifier name) =>
        new() { Kind = ChangeKind.Remove, Name = name };

    /// <summary>
    /// A comment change applied in place — the only modification a key takes without being recreated.
    /// </summary>
    public static PrimaryKeyDiff CommentChanged(SqlIdentifier name, ValueChange<string> comment) =>
        new() { Kind = ChangeKind.Modify, Name = name, Comment = comment };
}
