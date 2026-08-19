using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using NSchema.Model;
using NSchema.Model.XmlSchemaCollections;

namespace NSchema.Diff.Domain.XmlSchemaCollections;

/// <summary>
/// Describes a change to an XML schema collection.
/// </summary>
/// <remarks>
/// A collection's body only ever grows in place — an engine can add namespaces to one but not take them away —
/// so a change to what it holds is a drop and a recreate rather than an alteration, and the diff carries the
/// definition to rebuild it from.
/// </remarks>
public sealed record XmlSchemaCollectionDiff : ISchemaObjectDiff
{
    [JsonConstructor]
    private XmlSchemaCollectionDiff() { }

    /// <summary>
    /// The name of the schema the collection belongs to.
    /// </summary>
    public required SqlIdentifier Schema { get; init; }

    /// <inheritdoc />
    [JsonIgnore]
    public ObjectAddress Address => new(Schema, Name, SchemaObjectKind.XmlSchemaCollection);

    /// <summary>
    /// The collection name.
    /// </summary>
    public required SqlIdentifier Name { get; init; }

    /// <summary>
    /// The change to the collection.
    /// </summary>
    public required ChangeKind Change { get; init; }

    /// <summary>
    /// The previous name when renamed; otherwise <see langword="null"/>.
    /// </summary>
    public SqlIdentifier? RenamedFrom { get; init; }

    /// <summary>
    /// The definition for an added or rebuilt collection; otherwise <see langword="null"/>.
    /// </summary>
    public XmlSchemaCollection? Definition { get; init; }

    /// <summary>
    /// Whether the change must be applied as a drop and a recreate.
    /// </summary>
    public bool RequiresRecreate { get; init; }

    /// <summary>
    /// The change to the collection's comment, if any.
    /// </summary>
    public ValueChange<string>? Comment { get; init; }

    /// <summary>
    /// Whether this is a collection being created, and so carries the <see cref="Definition"/> to create it from.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Definition))]
    public bool IsAdd() => Change == ChangeKind.Add && Definition is not null;

    /// <summary>
    /// A collection being created, named by its own definition.
    /// </summary>
    public static XmlSchemaCollectionDiff Added(SqlIdentifier schema, XmlSchemaCollection definition) => new()
    {
        Schema = schema,
        Name = definition.Name,
        Change = ChangeKind.Add,
        Definition = definition,
        Comment = ValueChange.Between(null, definition.Comment),
    };

    /// <summary>
    /// A collection being dropped.
    /// </summary>
    public static XmlSchemaCollectionDiff Removed(SqlIdentifier schema, SqlIdentifier name) =>
        new() { Schema = schema, Name = name, Change = ChangeKind.Remove };

    /// <summary>
    /// A collection altered in place; the individual changes are set on the result.
    /// </summary>
    public static XmlSchemaCollectionDiff Modified(SqlIdentifier schema, SqlIdentifier name) =>
        new() { Schema = schema, Name = name, Change = ChangeKind.Modify };
}
