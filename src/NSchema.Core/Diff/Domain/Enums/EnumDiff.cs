using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using NSchema.Model;
using NSchema.Model.Enums;

namespace NSchema.Diff.Domain.Enums;

/// <summary>
/// Describes a change to an enum type.
/// </summary>
public sealed record EnumDiff : ISchemaObjectDiff
{
    [JsonConstructor]
    private EnumDiff() { }

    /// <summary>
    /// The name of the schema the enum type belongs to.
    /// </summary>
    public required SqlIdentifier Schema { get; init; }

    /// <inheritdoc />
    [JsonIgnore]
    public ObjectAddress Address => new(Schema, Name, ObjectKind.Enum);

    /// <summary>
    /// The enum type name.
    /// </summary>
    public required SqlIdentifier Name { get; init; }

    /// <summary>
    /// The change to the enum type.
    /// </summary>
    public required ChangeKind Kind { get; init; }

    /// <summary>
    /// The previous name when renamed; otherwise <see langword="null"/>.
    /// </summary>
    public SqlIdentifier? RenamedFrom { get; init; }

    /// <summary>
    /// The definition for an added enum type; otherwise <see langword="null"/>.
    /// </summary>
    public EnumType? Definition { get; init; }

    /// <summary>
    /// The values being added for a value-compatible modification, in execution order.
    /// </summary>
    public IReadOnlyList<EnumValueAddition> AddedValues { get; init; } = [];

    /// <summary>
    /// The change to the value list, set whenever it changed at all (including changes that cannot be planned), so drift can display it.
    /// </summary>
    public ValueChange<IReadOnlyList<EnumLabel>>? Values { get; init; }

    /// <summary>
    /// The change to the enum type's comment, if any.
    /// </summary>
    public ValueChange<string>? Comment { get; init; }

    /// <summary>
    /// Whether this is a enum type being created, and so carries the <see cref="Definition"/> to create it from.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Definition))]
    public bool IsAdd() => Kind == ChangeKind.Add && Definition is not null;

    /// <summary>
    /// An enum type being created, named by its own definition.
    /// </summary>
    public static EnumDiff Added(SqlIdentifier schema, EnumType definition) => new()
    {
        Schema = schema,
        Name = definition.Name,
        Kind = ChangeKind.Add,
        Definition = definition,
        Comment = ValueChange.Between(null, definition.Comment),
    };

    /// <summary>
    /// An enum type being dropped.
    /// </summary>
    public static EnumDiff Removed(SqlIdentifier schema, SqlIdentifier name) => new()
    {
        Schema = schema,
        Name = name,
        Kind = ChangeKind.Remove
    };

    /// <summary>
    /// An enum type altered in place; the individual changes are set on the result.
    /// </summary>
    public static EnumDiff Modified(SqlIdentifier schema, SqlIdentifier name) => new()
    {
        Schema = schema,
        Name = name,
        Kind = ChangeKind.Modify
    };

    /// <summary>
    /// The value list changed but cannot be expressed as additions — a value was removed or reordered. Planning
    /// such a change is rejected; the type must be recreated manually.
    /// </summary>
    public bool RequiresRecreate => Values is not null && AddedValues.Count == 0;
}
