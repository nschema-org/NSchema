using System.Text.Json.Serialization;

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
