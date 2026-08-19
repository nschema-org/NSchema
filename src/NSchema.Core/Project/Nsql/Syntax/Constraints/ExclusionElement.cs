using NSchema.Model;
using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax.Constraints;

/// <summary>
/// A single element of an exclusion constraint: a column name or a raw expression, with its operator.
/// </summary>
/// <param name="Operator">The exclusion operator (e.g. <c>=</c>, <c>&amp;&amp;</c>).</param>
/// <param name="Column">The column name; exactly one of <paramref name="Column"/> and <paramref name="Expression"/> is given.</param>
/// <param name="Expression">The raw element expression; exactly one of <paramref name="Column"/> and <paramref name="Expression"/> is given.</param>
public sealed record ExclusionElement(string Operator, Identifier? Column = null, SqlText? Expression = null) : NsqlNode
{
    /// <summary>
    /// The <c>(</c> token, when parsed with an expression element.
    /// </summary>
    public Token? OpenParenToken { get; init; }

    /// <summary>
    /// The verbatim expression span token, when parsed with an expression element.
    /// </summary>
    public Token? ExpressionToken { get; init; }

    /// <summary>
    /// The <c>)</c> token, when parsed with an expression element.
    /// </summary>
    public Token? CloseParenToken { get; init; }

    /// <summary>
    /// The verbatim <c>WITH operator</c> span token.
    /// </summary>
    public Token WithOperatorToken { get; init; } = Token.Span($"{NsqlKeywords.With} {Operator}");

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
            yield return WithOperatorToken;
        }
    }
}
