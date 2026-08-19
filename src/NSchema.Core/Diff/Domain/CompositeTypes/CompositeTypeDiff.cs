using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using NSchema.Model;
using NSchema.Model.CompositeTypes;

namespace NSchema.Diff.Domain.CompositeTypes;

/// <summary>
/// Describes a change to a composite type.
/// </summary>
public sealed record CompositeTypeDiff : ISchemaObjectDiff
{
    [JsonConstructor]
    private CompositeTypeDiff() { }

    /// <summary>
    /// The name of the schema the composite type belongs to.
    /// </summary>
    public required SqlIdentifier Schema { get; init; }

    /// <inheritdoc />
    [JsonIgnore]
    public ObjectAddress Address => ObjectAddress.CompositeType(Schema, Name);

    /// <summary>
    /// The composite type name.
    /// </summary>
    public required SqlIdentifier Name { get; init; }

    /// <summary>
    /// The change to the composite type.
    /// </summary>
    public required ChangeKind Change { get; init; }

    /// <summary>
    /// The previous name when renamed; otherwise <see langword="null"/>.
    /// </summary>
    public SqlIdentifier? RenamedFrom { get; init; }

    /// <summary>
    /// The definition for an added composite type; otherwise <see langword="null"/>.
    /// </summary>
    public CompositeType? Definition { get; init; }

    /// <summary>
    /// In-place field changes (added/dropped/retyped via <c>ALTER TYPE</c>) on an existing type.
    /// </summary>
    public IReadOnlyList<CompositeFieldDiff> Fields { get; init; } = [];

    /// <summary>
    /// The change to the composite type's comment, if any.
    /// </summary>
    public ValueChange<string>? Comment { get; init; }

    /// <summary>
    /// Whether this is a composite type being created, and so carries the <see cref="Definition"/> to create it from.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Definition))]
    public bool IsAdd() => Change == ChangeKind.Add && Definition is not null;

    /// <summary>
    /// A composite type being created, named by its own definition.
    /// </summary>
    public static CompositeTypeDiff Added(SqlIdentifier schema, CompositeType definition) => new()
    {
        Schema = schema,
        Name = definition.Name,
        Change = ChangeKind.Add,
        Definition = definition,
        Comment = ValueChange.Between(null, definition.Comment),
    };

    /// <summary>
    /// A composite type being dropped.
    /// </summary>
    public static CompositeTypeDiff Removed(SqlIdentifier schema, SqlIdentifier name) => new()
    {
        Schema = schema,
        Name = name,
        Change = ChangeKind.Remove
    };

    /// <summary>
    /// A composite type altered in place; the individual changes are set on the result.
    /// </summary>
    public static CompositeTypeDiff Modified(SqlIdentifier schema, SqlIdentifier name) => new()
    {
        Schema = schema,
        Name = name,
        Change = ChangeKind.Modify
    };
}
