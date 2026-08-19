using System.Diagnostics;

namespace NSchema.Model.XmlSchemaCollections;

/// <summary>
/// A named collection of XSD schemas a typed <c>xml</c> column is validated against.
/// </summary>
/// <remarks>
/// The collection is one object however many namespaces it holds: an engine merges what is added to it and
/// reports the whole thing back as a single document, so the body is opaque text like a view's, not a list.
/// </remarks>
[DebuggerDisplay("{Name,nq} (xml schema collection)")]
public sealed class XmlSchemaCollection : SchemaObject, IEquatable<XmlSchemaCollection>
{
    /// <inheritdoc/>
    public override SchemaObjectKind Kind => SchemaObjectKind.XmlSchemaCollection;

    /// <summary>
    /// The XSD the collection holds, verbatim.
    /// </summary>
    public required SqlText Body { get; set; }

    /// <inheritdoc/>
    public override XmlSchemaCollection Clone() => new()
    {
        Name = Name,
        Body = Body,
        ProvidedBy = ProvidedBy,
        Comment = Comment,
    };

    /// <summary>
    /// Structural equality over the declared definition; the schema and the comment are excluded.
    /// </summary>
    public bool Equals(XmlSchemaCollection? other) =>
        other is not null
        && Name == other.Name
        && Body == other.Body;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is XmlSchemaCollection other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Name, Body);
}
