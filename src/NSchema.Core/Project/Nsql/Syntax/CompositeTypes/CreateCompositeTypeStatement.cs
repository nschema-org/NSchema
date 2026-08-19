using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax.CompositeTypes;

/// <summary>
/// <c>CREATE TYPE schema.name [RENAMED FROM old] AS (field type, …);</c>
/// </summary>
/// <param name="Name">The composite type name as written.</param>
/// <param name="Fields">The fields in declaration order.</param>
public sealed record CreateCompositeTypeStatement(
    QualifiedName Name,
    SeparatedSyntaxList<CompositeFieldDefinition> Fields
) : NsqlStatement
{
    /// <summary>
    /// The <c>CREATE</c> keyword token.
    /// </summary>
    public Token CreateKeyword { get; init; } = Token.Keyword(NsqlKeywords.Create);

    /// <summary>
    /// The <c>TYPE</c> keyword token.
    /// </summary>
    public Token TypeKeyword { get; init; } = Token.Keyword(NsqlKeywords.Type);

    /// <summary>
    /// The <c>AS</c> keyword token.
    /// </summary>
    public Token AsKeyword { get; init; } = Token.Keyword(NsqlKeywords.As);

    /// <summary>
    /// The <c>(</c> token opening the field list.
    /// </summary>
    public Token OpenParenToken { get; init; } = Token.Punctuation(TokenKind.LeftParen, NsqlSymbols.LeftParen);

    /// <summary>
    /// The <c>)</c> token closing the field list.
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
            yield return TypeKeyword;
            yield return Name;
            yield return AsKeyword;
            yield return OpenParenToken;
            foreach (var child in Fields.Children)
            {
                yield return child;
            }
            yield return CloseParenToken;
            yield return SemicolonToken;
        }
    }
}
