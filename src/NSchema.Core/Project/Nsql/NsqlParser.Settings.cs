using System.Globalization;
using NSchema.Project.Nsql.Syntax;
using NSchema.Project.Nsql.Syntax.Settings;
using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql;

internal sealed partial class NsqlParser
{
    /// <summary>The settings keyword the current token leads with, or <see langword="null"/> when it is none.</summary>
    private SettingsKeyword? CurrentSettingsKeyword() =>
        _current.IsKeyword(NsqlKeywords.Plugin) ? SettingsKeyword.Plugin
        : _current.IsKeyword(NsqlKeywords.Engine) ? SettingsKeyword.Engine
        : _current.IsKeyword(NsqlKeywords.Database) ? SettingsKeyword.Database
        : _current.IsKeyword(NsqlKeywords.State) ? SettingsKeyword.State
        : _current.IsKeyword(NsqlKeywords.Lock) ? SettingsKeyword.Lock
        : null;

    /// <summary>
    /// Parses a settings statement — <c>KEYWORD [label] ( key = value , … ) ;</c> — and enforces the keyword's label rule
    /// (PLUGIN requires one; ENGINE and LOCK forbid one; DATABASE and STATE are optional).
    /// </summary>
    private SettingsStatement ParseSettingsStatement(SettingsKeyword keyword, Token? doc)
    {
        var body = ParseSettingsBody();
        switch (keyword)
        {
            case SettingsKeyword.Plugin when body.Label is null:
                throw new NsqlSyntaxException("A PLUGIN statement requires a label naming the plugin, e.g. PLUGIN pg ( … ).", body.Keyword.Position);
            case SettingsKeyword.Engine when body.Label is not null:
                throw new NsqlSyntaxException("An ENGINE statement takes no label; there is only one engine.", body.Label.Position);
            case SettingsKeyword.Lock when body.Label is not null:
                throw new NsqlSyntaxException("A LOCK statement takes no label; it identifies its package by the 'source' attribute.", body.Label.Position);
        }
        return new SettingsStatement(keyword, body.Label, body.Settings)
        {
            Doc = doc?.Text,
            DocComment = doc,
            KeywordToken = body.Keyword,
            OpenParenToken = body.Open,
            CloseParenToken = body.Close,
            SemicolonToken = body.Semicolon,
        };
    }

    /// <summary>
    /// Parses the shape every settings statement shares: <c>KEYWORD [label] ( key = value , … ) ;</c>.
    /// </summary>
    private (Token Keyword, Identifier? Label, Token Open, SeparatedSyntaxList<Setting> Settings, Token Close, Token Semicolon) ParseSettingsBody()
    {
        var keyword = Advance(); // the settings keyword

        // An optional bare-identifier label, e.g. the 'postgres' in `DATABASE postgres ( … )`.
        Identifier? label = null;
        if (_current.Kind == TokenKind.Identifier)
        {
            var token = Advance();
            label = new Identifier(token);
        }

        var open = Expect(TokenKind.LeftParen, "'(' to begin the settings list");
        var settings = new List<Setting>();
        var separators = new List<Token>();
        if (_current.Kind != TokenKind.RightParen)
        {
            do
            {
                var keyStart = _current;
                var key = ParseSettingKey();
                var keySpan = RawSpanFrom(keyStart, _current);
                if (settings.Any(a => string.Equals(a.Key, key, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new NsqlSyntaxException($"Attribute '{key}' is specified more than once.", keyStart.Position);
                }
                var equals = Expect(TokenKind.Equals, "'=' after an attribute name");
                var valueStart = _current;
                var value = ParseSettingValue();
                var valueSpan = RawSpanFrom(valueStart, _current);
                settings.Add(new Setting(key, value)
                {
                    KeyToken = keySpan,
                    EqualsToken = equals,
                    ValueToken = valueSpan,
                });
            }
            while (TryConsumeSeparator(TokenKind.Comma, separators));
        }
        var close = Expect(TokenKind.RightParen, "')' or ',' after an attribute");
        var semicolon = Expect(TokenKind.Semicolon, "';'");

        return (keyword, label, open, new SeparatedSyntaxList<Setting>(settings, separators), close, semicolon);
    }

    /// <summary>
    /// Parses a (possibly dotted) attribute name, e.g. <c>path</c> or <c>pool.max</c>.
    /// </summary>
    private string ParseSettingKey()
    {
        var key = ExpectIdentifierNode("an attribute name").Value;
        while (Match(TokenKind.Dot))
        {
            key += NsqlSymbols.Dot + ExpectIdentifierNode("an attribute name segment").Value;
        }
        return key;
    }

    /// <summary>
    /// Parses a scalar — a string, (signed) integer, <c>true</c>/<c>false</c>, or bare identifier — as the text
    /// it was written as. The binder converts it to the attribute's target type.
    /// </summary>
    private string ParseSettingValue()
    {
        switch (_current.Kind)
        {
            case TokenKind.String:
            case TokenKind.Identifier:
                return Advance().Text;
            case TokenKind.Integer:
            case TokenKind.Minus:
                return ExpectSignedIntegerValue().ToString(CultureInfo.InvariantCulture);
            default:
                throw Error("Expected a value (a string, integer, true, false, or identifier).");
        }
    }

    // Reads a value written as true/false; a parse-time check for the few options (e.g. run_outside_transaction)
    // the parser resolves directly rather than through the binder.
    private static bool AsBoolean(string value, SourcePosition position) => bool.TryParse(value, out var result)
        ? result
        : throw new NsqlSyntaxException($"Expected 'true' or 'false', but got '{value}'.", position);
}
