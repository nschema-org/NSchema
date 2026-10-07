using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax.Publications;

/// <summary>
/// <c>RENAME PUBLICATION name TO name;</c>
/// </summary>
/// <param name="From">The publication's current name.</param>
/// <param name="To">The name the publication is renamed to.</param>
public sealed record RenamePublicationStatement(Identifier From, Identifier To) : NsqlStatement
{
    /// <summary>
    /// The <c>RENAME</c> keyword token.
    /// </summary>
    public Token RenameKeyword { get; init; } = Token.Keyword(NsqlKeywords.Rename);

    /// <summary>
    /// The <c>PUBLICATION</c> keyword token.
    /// </summary>
    public Token PublicationKeyword { get; init; } = Token.Keyword(NsqlKeywords.Publication);

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
            yield return PublicationKeyword;
            yield return From;
            yield return ToKeyword;
            yield return To;
            yield return SemicolonToken;
        }
    }
}
