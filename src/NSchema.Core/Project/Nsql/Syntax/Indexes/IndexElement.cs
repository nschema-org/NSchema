using NSchema.Model;
using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax.Indexes;

/// <summary>
/// A single index key: a column name or a raw expression, with its ordering.
/// </summary>
/// <param name="Column">The column name; exactly one of <paramref name="Column"/> and <paramref name="Expression"/> is given.</param>
/// <param name="Expression">The raw key expression; exactly one of <paramref name="Column"/> and <paramref name="Expression"/> is given.</param>
/// <param name="Sort">The sort direction; <see cref="IndexSort.Default"/> when unwritten.</param>
/// <param name="Nulls">Where nulls sort; <see cref="IndexNulls.Default"/> when unwritten.</param>
public sealed record IndexElement(
    Identifier? Column = null,
    SqlText? Expression = null,
    IndexSort Sort = IndexSort.Default,
    IndexNulls Nulls = IndexNulls.Default
) : NsqlNode
{
    /// <summary>
    /// The <c>(</c> token, when parsed with an expression key.
    /// </summary>
    public Token? OpenParenToken { get; init; }

    /// <summary>
    /// The verbatim expression span token, when parsed with an expression key.
    /// </summary>
    public Token? ExpressionToken { get; init; }

    /// <summary>
    /// The <c>)</c> token, when parsed with an expression key.
    /// </summary>
    public Token? CloseParenToken { get; init; }

    /// <summary>
    /// The <c>ASC</c>/<c>DESC</c> token, when parsed with a sort direction.
    /// </summary>
    public Token? SortToken { get; init; }

    /// <summary>
    /// The <c>NULLS</c> keyword token, when parsed with a null ordering.
    /// </summary>
    public Token? NullsKeyword { get; init; }

    /// <summary>
    /// The <c>FIRST</c>/<c>LAST</c> token, when parsed with a null ordering.
    /// </summary>
    public Token? NullsPositionToken { get; init; }

    internal override IEnumerable<NsqlChild> Children
    {
        get
        {
            if (Column is { } column)
            {
                yield return column;
            }
            else if (Expression is { } expression)
            {
                yield return OpenParenToken ?? Token.Punctuation(TokenKind.LeftParen, NsqlSymbols.LeftParen);
                yield return ExpressionToken ?? Token.Span(expression.Value);
                yield return CloseParenToken ?? Token.Punctuation(TokenKind.RightParen, NsqlSymbols.RightParen);
            }
            if (Sort != IndexSort.Default)
            {
                yield return SortToken ?? Token.Keyword(Sort == IndexSort.Descending ? NsqlKeywords.Desc : NsqlKeywords.Asc);
            }
            if (Nulls != IndexNulls.Default)
            {
                yield return NullsKeyword ?? Token.Keyword(NsqlKeywords.Nulls);
                yield return NullsPositionToken ?? Token.Keyword(Nulls == IndexNulls.First ? NsqlKeywords.First : NsqlKeywords.Last);
            }
        }
    }
}
