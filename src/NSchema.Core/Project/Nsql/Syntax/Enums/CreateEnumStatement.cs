using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax.Enums;

/// <summary>
/// <c>CREATE ENUM schema.name [RENAMED FROM old] ('value', …);</c>
/// </summary>
/// <param name="Name">The enum name as written.</param>
/// <param name="Values">The values in declaration order.</param>
public sealed record CreateEnumStatement(
    QualifiedName Name,
    SeparatedSyntaxList<EnumValue> Values
) : NsqlStatement
{
    /// <summary>
    /// The <c>CREATE</c> keyword token.
    /// </summary>
    public Token CreateKeyword { get; init; } = Token.Keyword(NsqlKeywords.Create);

    /// <summary>
    /// The <c>ENUM</c> keyword token.
    /// </summary>
    public Token EnumKeyword { get; init; } = Token.Keyword(NsqlKeywords.Enum);

    /// <summary>
    /// The <c>(</c> token opening the value list.
    /// </summary>
    public Token OpenParenToken { get; init; } = Token.Punctuation(TokenKind.LeftParen, NsqlSymbols.LeftParen);

    /// <summary>
    /// The <c>)</c> token closing the value list.
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
            yield return EnumKeyword;
            yield return Name;
            yield return OpenParenToken;
            foreach (var child in Values.Children)
            {
                yield return child;
            }
            yield return CloseParenToken;
            yield return SemicolonToken;
        }
    }
}
