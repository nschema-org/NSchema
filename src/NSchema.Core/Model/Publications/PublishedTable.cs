namespace NSchema.Model.Publications;

/// <summary>
/// A table a publication publishes, optionally narrowed to some of its columns and rows.
/// </summary>
/// <param name="Table">The published table.</param>
/// <param name="Columns">The published columns, or <see langword="null"/> for all of them.</param>
/// <param name="Filter">The condition a row must meet to be published, or <see langword="null"/> for every row.</param>
public sealed record PublishedTable(ObjectAddress Table, IReadOnlyList<SqlIdentifier>? Columns = null, SqlText? Filter = null)
{
    /// <summary>
    /// The columns the entry names, in its column list or, by a scan, in its filter.
    /// </summary>
    internal IEnumerable<(SqlIdentifier Column, bool Stated)> References() =>
        (Columns ?? []).Select(c => (c, true))
        .Concat(Filter is { } filter
            ? Services.ExpressionDependencyExtractor.Names(filter.Value).Select(c => (c, false))
            : []);

    /// <summary>
    /// Whether the entry publishes the same columns as <paramref name="other"/>, in whatever order either lists them.
    /// </summary>
    public bool SameColumns(PublishedTable other) =>
        Columns is null ? other.Columns is null : other.Columns is not null && Columns.ToHashSet().SetEquals(other.Columns);

    /// <summary>
    /// Structural equality; the column list is a set, and the table a reference, so its kind is not compared.
    /// </summary>
    public bool Equals(PublishedTable? other) =>
        other is not null
        && Table.Schema == other.Table.Schema
        && Table.Name == other.Table.Name
        && SameColumns(other)
        && Filter == other.Filter;

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Table.Schema, Table.Name, Columns?.Count, Filter);
}
