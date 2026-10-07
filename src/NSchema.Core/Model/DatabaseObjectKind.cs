namespace NSchema.Model;

/// <summary>
/// The kind of an object the database owns directly.
/// </summary>
public enum DatabaseObjectKind
{
    /// <summary>
    /// A database schema, that contains tables and other objects..
    /// </summary>
    Schema,

    /// <summary>
    /// A database extension.
    /// </summary>
    Extension,

    /// <summary>
    /// A publication that describes table data the database publishes..
    /// </summary>
    Publication
}
