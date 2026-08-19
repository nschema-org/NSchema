namespace NSchema.Model.Columns;

/// <summary>
/// The XML schema collection a typed <c>xml</c> value is validated against, and what shape it must take.
/// </summary>
/// <remarks>
/// An untyped <c>xml</c> value is well-formed and nothing more. Bound to a collection it is validated, and its
/// contents become typed..
/// </remarks>
/// <param name="Collection">The collection the value is validated against.</param>
/// <param name="IsDocument">
/// Whether the value must be a single well-formed document (<c>DOCUMENT</c>) rather than any well-formed
/// fragment (<c>CONTENT</c>, the default).
/// </param>
public sealed record XmlTypeBinding(ObjectAddress Collection, bool IsDocument = false);
