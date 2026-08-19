using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax.Extensions;

/// <summary>
/// <c>CREATE EXTENSION name [VERSION 'version'];</c> — the name may be written bare or quoted
/// (<c>'uuid-ossp'</c>) since extension names commonly contain characters a bare identifier cannot.
/// </summary>
/// <param name="Name">The extension name.</param>
/// <param name="Version">The <c>VERSION</c> string, or <see langword="null"/>.</param>
public sealed record CreateExtensionStatement(Identifier Name, string? Version = null) : NsqlStatement
{
    /// <summary>
    /// The <c>CREATE</c> keyword token.
    /// </summary>
    public Token CreateKeyword { get; init; } = Token.Keyword(NsqlKeywords.Create);

    /// <summary>
    /// The <c>EXTENSION</c> keyword token.
    /// </summary>
    public Token ExtensionKeyword { get; init; } = Token.Keyword(NsqlKeywords.Extension);

    /// <summary>
    /// The <c>VERSION</c> keyword token, when parsed with a version clause.
    /// </summary>
    public Token? VersionKeyword { get; init; }

    /// <summary>
    /// The version string-literal token, when parsed with a version clause.
    /// </summary>
    public Token? VersionToken { get; init; }

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
            yield return ExtensionKeyword;
            yield return Name;
            if (Version is not null)
            {
                yield return VersionKeyword ?? Token.Keyword(NsqlKeywords.Version);
                if (VersionToken is { } versionToken)
                {
                    yield return versionToken;
                }
            }
            yield return SemicolonToken;
        }
    }
}
