namespace NSchema.Model.Indexes;

/// <summary>
/// Where nulls sort within an index key. <see cref="Default"/> means it was not specified, so the database default applies.
/// </summary>
public enum IndexNulls
{
    /// <summary>
    /// Unspecified; the database default applies.
    /// </summary>
    Default,

    /// <summary>
    /// Nulls sort first (<c>NULLS FIRST</c>).
    /// </summary>
    First,

    /// <summary>
    /// Nulls sort last (<c>NULLS LAST</c>).
    /// </summary>
    Last
}
