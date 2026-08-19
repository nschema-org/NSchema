using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax.Tables;

/// <summary>
/// <c>CREATE TABLE schema.name [RENAMED FROM old] ( members… );</c>
/// </summary>
/// <param name="Name">The table name as written.</param>
/// <param name="Members">The body members in declaration order (columns, constraints, indexes, includes).</param>
public sealed record CreateTableStatement(
    QualifiedName Name,
    SeparatedSyntaxList<TableMember> Members
) : NsqlStatement
{
    /// <summary>
    /// The <c>CREATE</c> keyword token.
    /// </summary>
    public Token CreateKeyword { get; init; } = Token.Keyword(NsqlKeywords.Create);

    /// <summary>
    /// The <c>TABLE</c> keyword token.
    /// </summary>
    public Token TableKeyword { get; init; } = Token.Keyword(NsqlKeywords.Table);

    /// <summary>
    /// The <c>(</c> token opening the body.
    /// </summary>
    public Token OpenParenToken { get; init; } = Token.Punctuation(TokenKind.LeftParen, NsqlSymbols.LeftParen);

    /// <summary>
    /// The <c>)</c> token closing the body.
    /// </summary>
    public Token CloseParenToken { get; init; } = Token.Punctuation(TokenKind.RightParen, NsqlSymbols.RightParen);

    /// <summary>
    /// The terminating <c>;</c> token.
    /// </summary>
    public Token SemicolonToken { get; init; } = Token.Punctuation(TokenKind.Semicolon, NsqlSymbols.Semicolon);

    internal override IEnumerable<NsqlChild> Children
    {
        get
        {
            if (DocComment is { } doc)
            {
                yield return doc;
            }
            yield return CreateKeyword;
            yield return TableKeyword;
            yield return Name;
            yield return OpenParenToken;
            foreach (var child in Members.Children)
            {
                yield return child;
            }
            yield return CloseParenToken;
            yield return SemicolonToken;
        }
    }
}
