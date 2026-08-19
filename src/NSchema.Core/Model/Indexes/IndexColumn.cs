namespace NSchema.Model.Indexes;

/// <summary>
/// A single key of an index: a column name or a raw key expression.
/// </summary>
public sealed record IndexColumn
{
    /// <param name="Column">The column name; exactly one of <paramref name="Column"/> and <paramref name="Expression"/> must be given.</param>
    /// <param name="Expression">The raw key expression (rendered parenthesized).</param>
    /// <param name="Sort">The sort direction; <see cref="IndexSort.Default"/> leaves it to the database default.</param>
    /// <param name="Nulls">Where nulls sort; <see cref="IndexNulls.Default"/> leaves it to the database default.</param>
    public IndexColumn(SqlIdentifier? Column = null, SqlText? Expression = null, IndexSort Sort = IndexSort.Default, IndexNulls Nulls = IndexNulls.Default)
    {
        if (Column is null == Expression is null)
        {
            throw new ArgumentException("An index key is a column name or an expression: exactly one must be given.");
        }
        this.Column = Column;
        this.Expression = Expression;
        this.Sort = Sort;
        this.Nulls = Nulls;
    }

    /// <summary>
    /// The column name, or <see langword="null"/> for an expression key.
    /// </summary>
    public SqlIdentifier? Column { get; init; }

    /// <summary>
    /// The raw key expression (rendered parenthesised), or <see langword="null"/> for a column key.
    /// </summary>
    public SqlText? Expression { get; init; }

    /// <summary>
    /// The sort direction; <see cref="IndexSort.Default"/> leaves it to the database default.
    /// </summary>
    public IndexSort Sort { get; init; }

    /// <summary>
    /// Where nulls sort; <see cref="IndexNulls.Default"/> leaves it to the database default.
    /// </summary>
    public IndexNulls Nulls { get; init; }

    /// <summary>
    /// A bare column name converts to a plain ascending index key with default null ordering, so the common case
    /// — a list of column names — can be written directly (e.g. <c>["id", "email"]</c>).
    /// </summary>
    // The explicit SqlIdentifier is load-bearing: a bare string binds the record's synthesized copy
    // constructor through this very conversion (it fills no optional parameters, so it wins), recursing forever.
    public static implicit operator IndexColumn(string column) => new(new SqlIdentifier(column));
}
