using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax.Schemas;

/// <summary>
/// <c>GRANT USAGE ON SCHEMA name TO role;</c>
/// </summary>
/// <param name="Schema">The schema granted on.</param>
/// <param name="Role">The role granted to.</param>
public sealed record GrantSchemaUsageStatement(Identifier Schema, Identifier Role) : NsqlStatement
{
    /// <summary>
    /// The <c>GRANT</c> keyword token.
    /// </summary>
    public Token GrantKeyword { get; init; } = Token.Keyword(NsqlKeywords.Grant);

    /// <summary>
    /// The <c>USAGE</c> keyword token.
    /// </summary>
    public Token UsageKeyword { get; init; } = Token.Keyword(NsqlKeywords.Usage);

    /// <summary>
    /// The <c>ON</c> keyword token.
    /// </summary>
    public Token OnKeyword { get; init; } = Token.Keyword(NsqlKeywords.On);

    /// <summary>
    /// The <c>SCHEMA</c> keyword token.
    /// </summary>
    public Token SchemaKeyword { get; init; } = Token.Keyword(NsqlKeywords.Schema);

    /// <summary>
    /// The <c>TO</c> keyword token.
    /// </summary>
    public Token ToKeyword { get; init; } = Token.Keyword(NsqlKeywords.To);

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
            yield return GrantKeyword;
            yield return UsageKeyword;
            yield return OnKeyword;
            yield return SchemaKeyword;
            yield return Schema;
            yield return ToKeyword;
            yield return Role;
            yield return SemicolonToken;
        }
    }
}
