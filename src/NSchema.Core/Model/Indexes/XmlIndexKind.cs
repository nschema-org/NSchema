namespace NSchema.Model.Indexes;

/// <summary>
/// What an XML index indexes. An XML column holds a document rather than a value.
/// </summary>
public enum XmlIndexKind
{
    /// <summary>
    /// The node table itself: one row per node of every document in the column. Every other form is built over it.
    /// </summary>
    Primary,

    /// <summary>
    /// Keyed by path then value, for asking whether a known path exists.
    /// </summary>
    Path,

    /// <summary>
    /// Keyed by value then path, for finding a known value at an unknown path.
    /// </summary>
    Value,

    /// <summary>
    /// Keyed by the row then path and value, for reading several fields out of one document.
    /// </summary>
    Property,
}
