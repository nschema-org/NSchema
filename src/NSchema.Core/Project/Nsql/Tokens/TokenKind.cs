namespace NSchema.Project.Nsql.Tokens;

/// <summary>
/// The kinds of token the <see cref="NsqlLexer"/> produces for NSchema DDL. Keywords are not distinguished here —
/// they are lexed as <see cref="Identifier"/> and matched case-insensitively by the parser.
/// </summary>
public enum TokenKind
{
    /// <summary>
    /// An identifier or keyword (e.g. <c>users</c>, <c>CREATE</c>). Keywords are matched by the parser.
    /// </summary>
    Identifier,

    /// <summary>
    /// A delimited identifier, either double-quoted (<c>"Order Details"</c>; <c>""</c> escapes a quote) or
    /// bracketed (<c>[Order Details]</c>; <c>]]</c> escapes a bracket).
    /// </summary>
    QuotedIdentifier,

    /// <summary>
    /// A single-quoted string literal. The token text is the unescaped value (without the quotes).
    /// </summary>
    String,

    /// <summary>
    /// A run of digits. The token text is the raw digits.
    /// </summary>
    Integer,

    /// <summary>
    /// A doc-comment (<c>---</c> line or <c>/** … */</c> block). The token text is the trimmed comment body.
    /// </summary>
    DocComment,

    /// <summary>
    /// A dollar-quoted string (<c>$$ … $$</c> or <c>$tag$ … $tag$</c>).
    /// </summary>
    DollarString,

    /// <summary>
    /// A source line comment (<c>-- …</c>, not a <c>---</c> doc-comment), end-trimmed and keeping its <c>--</c>.
    /// </summary>
    /// <remarks>
    /// The lexer emits these as <see cref="Trivia"/>; the formatter reconstitutes them as tokens for its layout pass.
    /// </remarks>
    LineComment,

    /// <summary>
    /// A source block comment (<c>/* … */</c>, not a <c>/** … */</c> doc-comment), kept verbatim with its delimiters.
    /// </summary>
    /// <remarks>
    /// The lexer emits these as <see cref="Trivia"/>; the formatter reconstitutes them as tokens for its layout pass.
    /// </remarks>
    BlockComment,

    /// <summary>
    /// <c>(</c>
    /// </summary>
    LeftParen,

    /// <summary>
    /// <c>)</c>
    /// </summary>
    RightParen,

    /// <summary>
    /// <c>{</c>
    /// </summary>
    LeftBrace,

    /// <summary>
    /// <c>}</c>
    /// </summary>
    RightBrace,

    /// <summary>
    /// <c>,</c>
    /// </summary>
    Comma,

    /// <summary>
    /// <c>;</c>
    /// </summary>
    Semicolon,

    /// <summary>
    /// <c>.</c>
    /// </summary>
    Dot,

    /// <summary>
    /// <c>=</c>
    /// </summary>
    Equals,

    /// <summary>
    /// <c>-</c> (a sign on a numeric value; <c>--</c> starts a comment instead).
    /// </summary>
    Minus,

    /// <summary>
    /// Any other single punctuation character the lexer does not structurally recognise (e.g. <c>&gt;</c>, <c>&amp;</c>, <c>:</c>).
    /// </summary>
    Symbol,

    /// <summary>
    /// A verbatim opaque region (a view body, routine definition, default expression, …). The lexer never emits
    /// this; the parser mints it from a source slice so an opaque span reprints exactly.
    /// </summary>
    RawSpan,

    /// <summary>
    /// A zero-width placeholder for a grammatically-mandatory token that has not been supplied —
    /// the default of a non-nullable token slot until a parser or factory fills it.
    /// </summary>
    Missing,

    /// <summary>
    /// The end of the input.
    /// </summary>
    EndOfFile
}
