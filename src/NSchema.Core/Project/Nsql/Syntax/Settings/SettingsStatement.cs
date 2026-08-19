using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax.Settings;

/// <summary>
/// A settings statement: <c>KEYWORD [label] ( key = value, … );</c>. One shape for every keyword; the
/// <see cref="Keyword"/> says which. The configuration file and the lockfile are both sequences of these.
/// </summary>
/// <remarks>
/// Built through the factory for its keyword (<see cref="Database"/>, <see cref="State"/>, …) and refined with the
/// <c>With…</c> methods, so a statement whose keyword and label disagree cannot be expressed.
/// </remarks>
public sealed record SettingsStatement : NsqlStatement
{
    internal SettingsStatement(SettingsKeyword keyword, Identifier? label, SeparatedSyntaxList<Setting> settings)
    {
        Keyword = keyword;
        Label = label;
        Settings = settings;
        KeywordToken = Token.Keyword(KeywordText(keyword));
    }

    /// <summary>
    /// The keyword the statement leads with.
    /// </summary>
    public SettingsKeyword Keyword { get; }

    /// <summary>
    /// The bare label (e.g. the <c>postgres</c> in <c>DATABASE postgres (…)</c>), or <see langword="null"/> for a
    /// keyword that takes none.
    /// </summary>
    public Identifier? Label { get; }

    /// <summary>
    /// The settings the statement carries.
    /// </summary>
    public SeparatedSyntaxList<Setting> Settings { get; private init; }

    /// <summary>
    /// The statement's leading keyword token.
    /// </summary>
    public Token KeywordToken { get; init; }

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

    /// <summary>
    /// An <c>ENGINE</c> statement, which takes no label.
    /// </summary>
    public static SettingsStatement Engine() => new(SettingsKeyword.Engine, label: null, Empty);

    /// <summary
    /// >A <c>PLUGIN</c> declaration, labelled with the project's local name for the plugin.
    /// </summary>
    /// <param name="label">The local name the configuration refers to the plugin by.</param>
    public static SettingsStatement Plugin(string label) => Labelled(SettingsKeyword.Plugin, label);

    /// <summary>
    /// A <c>DATABASE</c> statement, labelled with the plugin it configures.
    /// </summary>
    /// <param name="label">The label of the declared <c>PLUGIN</c> this configures.</param>
    public static SettingsStatement Database(string label) => Labelled(SettingsKeyword.Database, label);

    /// <summary>
    /// A <c>STATE</c> statement, labelled with the plugin it configures (or <c>file</c> for the built-in store).
    /// </summary>
    /// <param name="label">The label of the declared <c>PLUGIN</c> this configures.</param>
    public static SettingsStatement State(string label) => Labelled(SettingsKeyword.State, label);

    /// <summary>
    /// A <c>LOCK</c> entry, which takes no label. The lockfile is a sequence of these.
    /// </summary>
    public static SettingsStatement Lock() => new(SettingsKeyword.Lock, label: null, Empty);

    /// <summary>
    /// This statement with <paramref name="key"/> set to <paramref name="value"/>.
    /// </summary>
    /// <param name="key">The setting key, which may be dotted (<c>pool.max</c>).</param>
    /// <param name="value">The setting value, as it should be written.</param>
    public SettingsStatement WithSetting(string key, string value)
    {
        var setting = new Setting(key, value);
        var index = IndexOf(key);

        var newSettings = new SeparatedSyntaxList<Setting>(index < 0
            ? [.. Settings, setting]
            : [.. Settings.Select((existing, i) => i == index ? setting : existing)]);

        return this with { Settings = newSettings };
    }

    /// <summary>
    /// This statement with every setting of <paramref name="overlay"/> applied over its own — how an environment
    /// overlay refines the statement it restates.
    /// </summary>
    /// <param name="overlay">The statement whose settings take precedence.</param>
    public SettingsStatement WithSettingsFrom(SettingsStatement overlay) =>
        overlay.Settings.Aggregate(this, (merged, setting) => merged.WithSetting(setting.Key, setting.Value));

    /// <summary>
    /// This statement carrying <paramref name="comment"/> as its doc-comment, which the language reads as the
    /// catalog comment for what follows.
    /// </summary>
    /// <param name="comment">The comment body, without the <c>---</c> markers. May span lines.</param>
    public SettingsStatement WithDocComment(string comment) =>
        this with { DocComment = new Token(TokenKind.DocComment, comment, SourcePosition.None) };

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

    // Keys bind case-insensitively, so they collide case-insensitively too.
    private int IndexOf(string key)
    {
        for (var i = 0; i < Settings.Count; i++)
        {
            if (string.Equals(Settings[i].Key, key, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private static SeparatedSyntaxList<Setting> Empty => new([]);

    private static SettingsStatement Labelled(SettingsKeyword keyword, string label) =>
        new(keyword, Identifier.Synthetic(label), Empty);

    private static string KeywordText(SettingsKeyword keyword) => keyword switch
    {
        SettingsKeyword.Plugin => NsqlKeywords.Plugin,
        SettingsKeyword.Engine => NsqlKeywords.Engine,
        SettingsKeyword.Database => NsqlKeywords.Database,
        SettingsKeyword.State => NsqlKeywords.State,
        _ => NsqlKeywords.Lock,
    };
}
