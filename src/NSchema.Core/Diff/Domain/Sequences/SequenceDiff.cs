using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using NSchema.Model;
using NSchema.Model.Sequences;

namespace NSchema.Diff.Domain.Sequences;

/// <summary>
/// Describes a change to a sequence.
/// </summary>
public sealed record SequenceDiff : ISchemaObjectDiff
{
    [JsonConstructor]
    private SequenceDiff() { }

    /// <summary>
    /// The name of the schema the sequence belongs to.
    /// </summary>
    public required SqlIdentifier Schema { get; init; }

    /// <inheritdoc />
    [JsonIgnore]
    public ObjectAddress Address => new(Schema, Name, SchemaObjectKind.Sequence);

    /// <summary>
    /// The sequence name.
    /// </summary>
    public required SqlIdentifier Name { get; init; }

    /// <summary>
    /// The change to the sequence.
    /// </summary>
    public required ChangeKind Change { get; init; }

    /// <summary>
    /// The previous name when renamed; otherwise <see langword="null"/>.
    /// </summary>
    public SqlIdentifier? RenamedFrom { get; init; }

    /// <summary>
    /// The definition for an added sequence; otherwise <see langword="null"/>.
    /// </summary>
    public Sequence? Definition { get; init; }

    /// <summary>
    /// The change to the sequence's options, if any.
    /// </summary>
    public ValueChange<SequenceOptions>? Options { get; init; }

    /// <summary>
    /// The change to the sequence's comment, if any.
    /// </summary>
    public ValueChange<string>? Comment { get; init; }

    /// <summary>
    /// Whether this is a sequence being created, and so carries the <see cref="Definition"/> to create it from.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Definition))]
    public bool IsAdd() => Change == ChangeKind.Add && Definition is not null;

    /// <summary>
    /// A sequence being created, named by its own definition.
    /// </summary>
    public static SequenceDiff Added(SqlIdentifier schema, Sequence definition) => new()
    {
        Schema = schema,
        Name = definition.Name,
        Change = ChangeKind.Add,
        Definition = definition,
        Comment = ValueChange.Between(null, definition.Comment),
    };

    /// <summary>
    /// A sequence being dropped.
    /// </summary>
    public static SequenceDiff Removed(SqlIdentifier schema, SqlIdentifier name) =>
        new() { Schema = schema, Name = name, Change = ChangeKind.Remove };

    /// <summary>
    /// A sequence altered in place; the individual changes are set on the result.
    /// </summary>
    public static SequenceDiff Modified(SqlIdentifier schema, SqlIdentifier name) =>
        new() { Schema = schema, Name = name, Change = ChangeKind.Modify };
}
