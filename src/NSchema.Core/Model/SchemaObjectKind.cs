namespace NSchema.Model;

/// <summary>
/// The kind of an object a schema owns: what sort of thing lives at an address.
/// </summary>
public enum SchemaObjectKind
{
    /// <summary>
    /// A table.
    /// </summary>
    Table,

    /// <summary>
    /// A view.
    /// </summary>
    View,

    /// <summary>
    /// An enum type.
    /// </summary>
    Enum,

    /// <summary>
    /// A sequence.
    /// </summary>
    Sequence,

    /// <summary>
    /// A routine (function or procedure).
    /// </summary>
    Routine,

    /// <summary>
    /// A domain.
    /// </summary>
    Domain,

    /// <summary>
    /// A composite type.
    /// </summary>
    CompositeType,

    /// <summary>
    /// A type provided by the engine or an extension.
    /// </summary>
    NativeType,

    /// <summary>
    /// A collection of XSD schemas a typed xml column is validated against.
    /// </summary>
    XmlSchemaCollection
}

/// <summary>
/// Rendering for <see cref="SchemaObjectKind"/>.
/// </summary>
internal static class SchemaObjectKindExtensions
{
    /// <summary>
    /// The kind as display prose (e.g. <c>"composite type"</c>), for diagnostics.
    /// </summary>
    public static string Display(this SchemaObjectKind kind) => kind switch
    {
        SchemaObjectKind.Table => "table",
        SchemaObjectKind.View => "view",
        SchemaObjectKind.Enum => "enum",
        SchemaObjectKind.Sequence => "sequence",
        SchemaObjectKind.Routine => "routine",
        SchemaObjectKind.Domain => "domain",
        SchemaObjectKind.CompositeType => "composite type",
        SchemaObjectKind.NativeType => "native type",
        SchemaObjectKind.XmlSchemaCollection => "xml schema collection",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };
}
