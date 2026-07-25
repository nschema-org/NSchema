using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using NSchema.Model;
using NSchema.Model.Triggers;

namespace NSchema.Diff.Domain.Triggers;

/// <summary>
/// Describes a change to a table trigger.
/// </summary>
public sealed record TriggerDiff : INamedObjectDiff
{
    [JsonConstructor]
    private TriggerDiff() { }

    /// <summary>
    /// The change to the trigger.
    /// </summary>
    public required ChangeKind Kind { get; init; }

    /// <summary>
    /// The trigger name.
    /// </summary>
    public required SqlIdentifier Name { get; init; }

    /// <summary>
    /// The definition for an added trigger; otherwise <see langword="null"/>.
    /// </summary>
    public Trigger? Definition { get; init; }

    /// <summary>
    /// The change to the comment, if any.
    /// </summary>
    public ValueChange<string>? Comment { get; init; }

    /// <summary>
    /// Whether this is a trigger being created, and so carries the <see cref="Definition"/> to create it from.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Definition))]
    public bool IsAdd() => Kind == ChangeKind.Add && Definition is not null;

    /// <summary>
    /// A trigger being created, named by its own definition.
    /// </summary>
    public static TriggerDiff Added(Trigger definition) =>
        new() { Kind = ChangeKind.Add, Name = definition.Name, Definition = definition };

    /// <summary>
    /// A trigger being dropped.
    /// </summary>
    public static TriggerDiff Removed(SqlIdentifier name) =>
        new() { Kind = ChangeKind.Remove, Name = name };

    /// <summary>
    /// A comment change applied in place.
    /// </summary>
    public static TriggerDiff CommentChanged(SqlIdentifier name, ValueChange<string> comment) =>
        new() { Kind = ChangeKind.Modify, Name = name, Comment = comment };
}
