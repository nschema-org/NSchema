using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax.Schemas;

/// <summary>
/// <c>CREATE [PARTIAL] SCHEMA name [RENAMED FROM old];</c>
/// </summary>
/// <param name="Name">The schema name.</param>
public sealed record CreateSchemaStatement(Identifier Name) : NsqlStatement
{
    /// <summary>
    /// The <c>CREATE</c> keyword token.
    /// </summary>
    public Token CreateKeyword { get; init; } = Token.Keyword(NsqlKeywords.Create);

    /// <summary>
    /// The <c>SCHEMA</c> keyword token.
    /// </summary>
    public Token SchemaKeyword { get; init; } = Token.Keyword(NsqlKeywords.Schema);

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
            yield return SchemaKeyword;
            yield return Name;
            yield return SemicolonToken;
        }
    }
}
