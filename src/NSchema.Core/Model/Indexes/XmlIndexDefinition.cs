namespace NSchema.Model.Indexes;

/// <summary>
/// The XML facet of an index: an index over the shredded contents of an XML column rather than over a value.
/// </summary>
/// <param name="Kind">Which form of the node table this index is.</param>
/// <param name="PrimaryIndex">
/// The primary XML index this one is built over, for every kind but <see cref="XmlIndexKind.Primary"/>.
/// A secondary indexes the primary's node table, not the column, so it cannot exist without it and cannot be created before it.
/// <see langword="null"/> for a primary, which is built over the column directly.
/// </param>
public sealed record XmlIndexDefinition(XmlIndexKind Kind, SqlIdentifier? PrimaryIndex = null)
{
    /// <summary>
    /// Whether this is the node table itself rather than an index over one.
    /// </summary>
    public bool IsPrimary => Kind == XmlIndexKind.Primary;
}
