namespace NSchema.Project.Nsql.Syntax.Indexes;

/// <summary>
/// An index key's null ordering, as written.
/// </summary>
public enum IndexNulls
{
    /// <summary>
    /// No null ordering written.
    /// </summary>
    Default,

    /// <summary>
    /// <c>NULLS FIRST</c>.
    /// </summary>
    First,

    /// <summary>
    /// <c>NULLS LAST</c>.
    /// </summary>
    Last
}
