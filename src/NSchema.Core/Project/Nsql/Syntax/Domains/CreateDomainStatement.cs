using NSchema.Model;
using NSchema.Project.Nsql.Syntax.Constraints;
using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax.Domains;

/// <summary>
/// <c>CREATE DOMAIN schema.name [RENAMED FROM old] AS type [NOT NULL | NULL] [CONSTRAINT n CHECK (e)]… [DEFAULT expr];</c>
/// </summary>
/// <param name="Name">The domain name as written.</param>
/// <param name="Type">The base type as written.</param>
/// <param name="NotNull">Whether the domain is declared <c>NOT NULL</c>.</param>
/// <param name="Checks">The named check constraints (empty when absent).</param>
/// <param name="Default">The <c>DEFAULT</c> expression, or <see langword="null"/>.</param>
public sealed record CreateDomainStatement(
    QualifiedName Name,
    TypeName Type,
    bool NotNull = false,
    IReadOnlyList<CheckDefinition>? Checks = null,
    SqlText? Default = null
) : NsqlStatement
{
    /// <summary>
    /// The named check constraints (empty when absent).
    /// </summary>
    public IReadOnlyList<CheckDefinition> Checks { get; init; } = Checks ?? [];

    /// <summary>
    /// The <c>CREATE</c> keyword token.
    /// </summary>
    public Token CreateKeyword { get; init; } = Token.Keyword(NsqlKeywords.Create);

    /// <summary>
    /// The <c>DOMAIN</c> keyword token.
    /// </summary>
    public Token DomainKeyword { get; init; } = Token.Keyword(NsqlKeywords.Domain);

    /// <summary>
    /// The <c>AS</c> keyword token.
    /// </summary>
    public Token AsKeyword { get; init; } = Token.Keyword(NsqlKeywords.As);

    /// <summary>
    /// The verbatim span of the clauses after the type (<c>NOT NULL</c>, checks, <c>DEFAULT</c>), when present.
    /// </summary>
    public Token? TailToken { get; init; }

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
            yield return DomainKeyword;
            yield return Name;
            yield return AsKeyword;
            yield return Type;
            if (TailToken is { } tail)
            {
                yield return tail;
            }
            yield return SemicolonToken;
        }
    }
}
