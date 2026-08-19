namespace NSchema.Project.Nsql.Tokens;

/// <summary>
/// The kinds of trivia the <see cref="NsqlLexer"/> attaches to tokens: the source text that carries no
/// syntactic meaning but must be preserved for a lossless round-trip. Doc-comments are tokens, not trivia.
/// </summary>
public enum TriviaKind
{
    /// <summary>
    /// A run of spaces and tabs (never crossing a line boundary).
    /// </summary>
    Whitespace,

    /// <summary>
    /// A single line break (<c>\n</c>, <c>\r\n</c>, or a lone <c>\r</c>).
    /// </summary>
    EndOfLine,

    /// <summary>
    /// A source line comment (<c>-- …</c>, not a <c>---</c> doc-comment), kept verbatim to the line's end.
    /// </summary>
    LineComment,

    /// <summary>
    /// A source block comment (<c>/* … */</c>, not a <c>/** … */</c> doc-comment), kept verbatim with its delimiters.
    /// </summary>
    BlockComment,

    /// <summary>
    /// A run of tokens the parser skipped recovering from a syntax error.
    /// The lexer never emits this; the parser attaches it to the next token so a document with errors still round-trips.
    /// </summary>
    Skipped,
}
