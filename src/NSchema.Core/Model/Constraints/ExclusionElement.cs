namespace NSchema.Model.Constraints;

/// <summary>
/// A single element of an exclusion constraint: a column name or a raw element expression.
/// </summary>
public sealed record ExclusionElement
{
    /// <param name="Operator">The exclusion operator (e.g. <c>=</c>, <c>&amp;&amp;</c>).</param>
    /// <param name="Column">The column name; exactly one of <paramref name="Column"/> and <paramref name="Expression"/> must be given.</param>
    /// <param name="Expression">The raw element expression (rendered parenthesized).</param>
    public ExclusionElement(string Operator, SqlIdentifier? Column = null, SqlText? Expression = null)
    {
        if (Column is null == Expression is null)
        {
            throw new ArgumentException("An exclusion element is a column name or an expression: exactly one must be given.");
        }
        this.Operator = Operator;
        this.Column = Column;
        this.Expression = Expression;
    }

    /// <summary>
    /// The exclusion operator (e.g. <c>=</c>, <c>&amp;&amp;</c>).
    /// </summary>
    public string Operator { get; init; }

    /// <summary>
    /// The column name, or <see langword="null"/> for an expression element.
    /// </summary>
    public SqlIdentifier? Column { get; init; }

    /// <summary>
    /// The raw element expression (rendered parenthesised), or <see langword="null"/> for a column element.
    /// </summary>
    public SqlText? Expression { get; init; }
}
