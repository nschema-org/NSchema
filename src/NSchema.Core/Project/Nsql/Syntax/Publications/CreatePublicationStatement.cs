using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax.Publications;

/// <summary>
/// <c>CREATE PUBLICATION name [FOR ALL TABLES | FOR target, …] [PUBLISH (operation, …)];</c>
/// </summary>
/// <param name="Name">The publication name.</param>
/// <param name="AllTables">Whether the publication publishes every table.</param>
/// <param name="Targets">The published tables and schemas, with their separators.</param>
/// <param name="Publish">The published operations, or <see langword="null"/> for all of them.</param>
public sealed record CreatePublicationStatement(
    Identifier Name,
    bool AllTables,
    SeparatedSyntaxList<PublicationTarget> Targets,
    PublishClause? Publish = null
) : NsqlStatement
{
    /// <summary>
    /// The <c>CREATE</c> keyword token.
    /// </summary>
    public Token CreateKeyword { get; init; } = Token.Keyword(NsqlKeywords.Create);

    /// <summary>
    /// The <c>PUBLICATION</c> keyword token.
    /// </summary>
    public Token PublicationKeyword { get; init; } = Token.Keyword(NsqlKeywords.Publication);

    /// <summary>
    /// The <c>FOR</c> keyword token, when parsed with a <c>FOR</c> clause.
    /// </summary>
    public Token? ForKeyword { get; init; }

    /// <summary>
    /// The <c>ALL</c> and <c>TABLES</c> keyword tokens, when parsed with <c>FOR ALL TABLES</c>.
    /// </summary>
    public IReadOnlyList<Token> AllTablesKeywords { get; init; } = [];

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
            yield return PublicationKeyword;
            yield return Name;
            if (AllTables || Targets.Count > 0)
            {
                yield return ForKeyword ?? Token.Keyword(NsqlKeywords.For);
            }
            if (AllTables)
            {
                if (AllTablesKeywords.Count > 0)
                {
                    foreach (var keyword in AllTablesKeywords)
                    {
                        yield return keyword;
                    }
                }
                else
                {
                    yield return Token.Keyword(NsqlKeywords.AllKeyword);
                    yield return Token.Keyword(NsqlKeywords.Tables);
                }
            }
            foreach (var child in Targets.Children)
            {
                yield return child;
            }
            if (Publish is not null)
            {
                yield return Publish;
            }
            yield return SemicolonToken;
        }
    }
}
