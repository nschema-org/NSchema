using System.Text;

namespace NSchema.Project.Nsql.Tokens;

/// <summary>
/// A single lexical token from a DDL source document.
/// </summary>
/// <param name="Kind">The kind of token.</param>
/// <param name="Text">
/// The token's textual payload: the raw identifier for <see cref="TokenKind.Identifier"/>, the unescaped value
/// for <see cref="TokenKind.String"/>, the digits for <see cref="TokenKind.Integer"/>, the trimmed comment body
/// for <see cref="TokenKind.DocComment"/>, the literal punctuation for the symbol kinds, and empty for
/// <see cref="TokenKind.EndOfFile"/>.
/// </param>
/// <param name="Position">Where the token begins in the source.</param>
public readonly record struct Token(TokenKind Kind, string Text, SourcePosition Position)
{
    private static readonly Trivia[] _noTrivia = [];

    /// <summary>
    /// The verbatim source text of the token. Defaults to <see cref="Text"/> for a synthetic token.
    /// </summary>
    public string Raw
    {
        get => field ?? Text;
        init;
    }

    /// <summary>
    /// The trivia before the token, up to the previous token's trailing trivia.
    /// </summary>
    public IReadOnlyList<Trivia> Leading
    {
        get => field ?? _noTrivia;
        init;
    }

    /// <summary>
    /// The trivia after the token, up to and including the first line break.
    /// </summary>
    public IReadOnlyList<Trivia> Trailing
    {
        get => field ?? _noTrivia;
        init;
    }

    /// <summary>
    /// A synthetic keyword token (an identifier carrying the keyword's canonical casing, no source backing).
    /// </summary>
    /// <param name="keyword">The keyword text.</param>
    public static Token Keyword(string keyword) => new(TokenKind.Identifier, keyword, SourcePosition.None);

    /// <summary>
    /// A synthetic punctuation token with no source backing.
    /// </summary>
    /// <param name="kind">The punctuation kind.</param>
    /// <param name="text">Its literal text.</param>
    public static Token Punctuation(TokenKind kind, string text) => new(kind, text, SourcePosition.None);

    /// <summary>
    /// A synthetic single-quoted string-literal token (text decoded, raw quoted and escaped).
    /// </summary>
    /// <param name="value">The decoded string value.</param>
    public static Token StringLiteral(string value) =>
        new(TokenKind.String, value, SourcePosition.None) { Raw = $"'{value.Replace("'", "''")}'" };

    /// <summary>
    /// A synthetic double-quoted identifier token (text decoded, raw quoted and escaped).
    /// </summary>
    /// <param name="value">The decoded identifier value.</param>
    public static Token QuotedIdentifier(string value) =>
        new(TokenKind.QuotedIdentifier, value, SourcePosition.None) { Raw = $"\"{value.Replace("\"", "\"\"")}\"" };

    /// <summary>
    /// A synthetic verbatim-span token whose raw text reprints as-is.
    /// </summary>
    /// <param name="text">The verbatim text.</param>
    public static Token Span(string text) => new(TokenKind.RawSpan, text, SourcePosition.None);

    /// <summary>
    /// A zero-width placeholder for a mandatory token slot that nothing has filled yet — prints as nothing.
    /// </summary>
    public static readonly Token Missing = new(TokenKind.Missing, "", SourcePosition.None);

    /// <summary>
    /// Whether this is the <see cref="Missing"/> placeholder for an unsupplied mandatory token.
    /// </summary>
    public bool IsMissing => Kind == TokenKind.Missing;

    /// <summary>
    /// Whether this token is an identifier whose text matches <paramref name="keyword"/>, case-insensitively.
    /// </summary>
    /// <param name="keyword">The keyword to test against.</param>
    public bool IsKeyword(string keyword) =>
        Kind == TokenKind.Identifier && NsqlKeywords.Comparer.Equals(Text, keyword);

    /// <summary>
    /// Whether this token matches any keyword in <paramref name="keywords"/> (a group from <see cref="NsqlKeywords"/>).
    /// </summary>
    /// <param name="keywords">The keyword group to test against.</param>
    public bool IsAnyKeyword(IReadOnlySet<string> keywords) => Kind == TokenKind.Identifier && keywords.Contains(Text);

    /// <summary>
    /// Whether this token matches any of <paramref name="keywords"/>.
    /// </summary>
    /// <param name="keywords">The keywords to test against.</param>
    public bool IsAnyKeyword(params ReadOnlySpan<string> keywords)
    {
        foreach (var keyword in keywords)
        {
            if (IsKeyword(keyword))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Whether this token can serve as an object name: a bare or quoted identifier.
    /// </summary>
    public bool IsName => Kind is TokenKind.Identifier or TokenKind.QuotedIdentifier;

    /// <summary>
    /// Writes the token back to source: leading trivia, then the raw text, then trailing trivia.
    /// </summary>
    internal void WriteTo(StringBuilder sb)
    {
        foreach (var trivia in Leading)
        {
            sb.Append(trivia.Text);
        }
        sb.Append(Raw);
        foreach (var trivia in Trailing)
        {
            sb.Append(trivia.Text);
        }
    }
}
