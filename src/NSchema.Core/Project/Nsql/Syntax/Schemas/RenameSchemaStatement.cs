using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax.Schemas;

/// <summary>
/// <c>RENAME SCHEMA name TO name;</c>
/// </summary>
/// <param name="From">The schema's current name.</param>
/// <param name="To">The name the schema is renamed to.</param>
public sealed record RenameSchemaStatement(Identifier From, Identifier To) : NsqlStatement
{
    /// <summary>
    /// The <c>RENAME</c> keyword token.
    /// </summary>
    public Token RenameKeyword { get; init; } = Token.Keyword(NsqlKeywords.Rename);

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
            yield return RenameKeyword;
            yield return SchemaKeyword;
            yield return From;
            yield return ToKeyword;
            yield return To;
            yield return SemicolonToken;
        }
    }
}
