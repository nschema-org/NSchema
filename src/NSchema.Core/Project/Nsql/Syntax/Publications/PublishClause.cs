using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax.Publications;

/// <summary>
/// <c>PUBLISH (operation, …)</c>: the row changes a publication publishes.
/// </summary>
/// <param name="Operations">The operation keywords, with their separators.</param>
public sealed record PublishClause(SeparatedSyntaxList<Identifier> Operations) : NsqlNode
{
    /// <summary>
    /// The <c>PUBLISH</c> keyword token.
    /// </summary>
    public Token PublishKeyword { get; init; } = Token.Keyword(NsqlKeywords.Publish);

    /// <summary>
    /// The <c>(</c> token.
    /// </summary>
    public Token OpenParenToken { get; init; } = Token.Punctuation(TokenKind.LeftParen, NsqlSymbols.LeftParen);

    /// <summary>
    /// The <c>)</c> token.
    /// </summary>
    public Token CloseParenToken { get; init; } = Token.Punctuation(TokenKind.RightParen, NsqlSymbols.RightParen);

    internal override IEnumerable<NsqlChild> Children
    {
        get
        {
            yield return PublishKeyword;
            yield return OpenParenToken;
            foreach (var child in Operations.Children)
            {
                yield return child;
            }
            yield return CloseParenToken;
        }
    }
}
