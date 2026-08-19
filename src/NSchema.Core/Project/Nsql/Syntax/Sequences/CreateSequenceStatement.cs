using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax.Sequences;

/// <summary>
/// <c>CREATE SEQUENCE schema.name [RENAMED FROM old] [(options)];</c>
/// </summary>
/// <param name="Name">The sequence name as written.</param>
/// <param name="Options">The options clause, or <see langword="null"/> when absent.</param>
public sealed record CreateSequenceStatement(
    QualifiedName Name,
    SequenceOptionsClause? Options = null
) : NsqlStatement
{
    /// <summary>
    /// The <c>CREATE</c> keyword token.
    /// </summary>
    public Token CreateKeyword { get; init; } = Token.Keyword(NsqlKeywords.Create);

    /// <summary>
    /// The <c>SEQUENCE</c> keyword token.
    /// </summary>
    public Token SequenceKeyword { get; init; } = Token.Keyword(NsqlKeywords.Sequence);

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
            yield return SequenceKeyword;
            yield return Name;
            if (Options is { } options)
            {
                yield return options;
            }
            yield return SemicolonToken;
        }
    }
}
