using System.Text.Json.Serialization;
using NSchema.Model.Extensions;

namespace NSchema.Model;

/// <summary>
/// Attributes common to every database element.
/// </summary>
public abstract class DatabaseElement
{
    /// <summary>
    /// The element's name.
    /// </summary>
    public required SqlIdentifier Name { get; set; }

    /// <summary>
    /// An optional comment or description for the element.
    /// </summary>
    public string? Comment { get; set; }

    /// <summary>
    /// Whether the element is here for reference only, rather than something NSchema manages.
    /// </summary>
    /// <remarks>
    /// An element is implicit either because nothing declared it (someone referenced <c>dbo.x</c>),
    /// or because the database owns it (Postgres' <c>public</c>, a captured native type).
    /// </remarks>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public virtual bool IsImplicit { get; init; }

    /// <summary>
    /// The extension observed to provide this element, or <see langword="null"/>.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public ExtensionReference? ProvidedBy { get; init; }

    /// <summary>
    /// Where the element lives.
    /// </summary>
    /// <exception cref="InvalidOperationException">The element is not in a tree, so it has no address.</exception>
    [JsonIgnore]
    public abstract Address Address { get; }

    /// <summary>
    /// Returns a deep copy of the element, outside any tree.
    /// </summary>
    public abstract DatabaseElement Clone();
}
