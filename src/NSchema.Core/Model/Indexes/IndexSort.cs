namespace NSchema.Model.Indexes;

/// <summary>
/// The sort direction of an index key. <see cref="Default"/> means the direction was not specified, so the
/// database default applies (ascending for B-tree) — it is rendered without an explicit <c>ASC</c>/<c>DESC</c>.
/// </summary>
public enum IndexSort
{
    /// <summary>The direction is unspecified; the database default applies.</summary>
    Default,

    /// <summary>Ascending (<c>ASC</c>).</summary>
    Ascending,

    /// <summary>Descending (<c>DESC</c>).</summary>
    Descending
}
