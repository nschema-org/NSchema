namespace NSchema.Model.Publications;

/// <summary>
/// Specifies the row changes that a publication publishes.
/// </summary>
[Flags]
public enum PublishedOperations
{
    /// <summary>
    /// Nothing is published.
    /// </summary>
    None = 0,

    /// <summary>
    /// Inserted rows.
    /// </summary>
    Insert = 1,

    /// <summary>
    /// Updated rows.
    /// </summary>
    Update = 2,

    /// <summary>
    /// Deleted rows.
    /// </summary>
    Delete = 4,

    /// <summary>
    /// Truncations.
    /// </summary>
    Truncate = 8,

    /// <summary>
    /// Every change.
    /// </summary>
    All = Insert | Update | Delete | Truncate,
}
