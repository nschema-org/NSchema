namespace NSchema.Project.Nsql.Syntax.Indexes;

/// <summary>
/// An index key's sort direction, as written.
/// </summary>
public enum IndexSort
{
    /// <summary>
    /// No direction written.
    /// </summary>
    Default,

    /// <summary>
    /// <c>ASC</c>.
    /// </summary>
    Ascending,

    /// <summary>
    /// <c>DESC</c>.
    /// </summary>
    Descending
}
