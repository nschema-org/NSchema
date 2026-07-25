using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax.Settings;

/// <summary>
/// A settings statement: <c>KEYWORD [label] ( key = value, … );</c>. One shape for every keyword; the
/// <paramref name="Keyword"/> says which. The configuration file and the lockfile are both sequences of these.
/// </summary>
/// <param name="Keyword">The keyword the statement leads with.</param>
/// <param name="Label">The optional bare label (e.g. the <c>postgres</c> in <c>DATABASE postgres (…)</c>).</param>
/// <param name="Settings">The attribute list.</param>
public sealed record SettingsStatement(SettingsKeyword Keyword, Identifier? Label, SeparatedSyntaxList<Setting> Settings) : NsqlStatement
{
    /// <summary>
    /// The statement's leading keyword token, when parsed.
    /// </summary>
    public Token KeywordToken { get; init; } = Token.Keyword(KeywordText(Keyword));

    /// <summary>
    /// The <c>(</c> token opening the settings.
    /// </summary>
    public Token OpenParenToken { get; init; } = Token.Punctuation(TokenKind.LeftParen, NsqlSymbols.LeftParen);

    /// <summary>
    /// The <c>)</c> token closing the settings.
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
            yield return KeywordToken;
            if (Label is { } label)
            {
                yield return label;
            }
            yield return OpenParenToken;
            foreach (var child in Settings.Children)
            {
                yield return child;
            }
            yield return CloseParenToken;
            yield return SemicolonToken;
        }
    }

    private static string KeywordText(SettingsKeyword keyword) => keyword switch
    {
        SettingsKeyword.Plugin => NsqlKeywords.Plugin,
        SettingsKeyword.Engine => NsqlKeywords.Engine,
        SettingsKeyword.Database => NsqlKeywords.Database,
        SettingsKeyword.State => NsqlKeywords.State,
        _ => NsqlKeywords.Lock,
    };
}
