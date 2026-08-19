using System.Text.Json.Serialization;

namespace NSchema.Model;

/// <summary>
/// Attributes common to the objects the database owns directly.
/// </summary>
public abstract class DatabaseObject : DatabaseElement
{
    /// <summary>
    /// The kind of object this is.
    /// </summary>
    [JsonIgnore]
    public abstract DatabaseObjectKind Kind { get; }
}
