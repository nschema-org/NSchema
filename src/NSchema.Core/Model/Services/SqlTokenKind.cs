namespace NSchema.Model.Services;

/// <summary>
/// The kinds of token a <see cref="SqlLexer"/> produces.
/// </summary>
public enum SqlTokenKind
{
    /// <summary>
    /// A bare word: an identifier, keyword, or variable — the only kind a keyword check may match.
    /// </summary>
    Word,

    /// <summary>
    /// A quoted or bracketed identifier; never a keyword, whatever it spells.
    /// </summary>
    QuotedIdentifier,

    /// <summary>
    /// A single-quoted string literal.
    /// </summary>
    String,

    /// <summary>
    /// A dollar-quoted block.
    /// </summary>
    DollarString,

    /// <summary>
    /// An opening parenthesis.
    /// </summary>
    LeftParen,

    /// <summary>
    /// A closing parenthesis.
    /// </summary>
    RightParen,

    /// <summary>
    /// A comma.
    /// </summary>
    Comma,

    /// <summary>
    /// A dot.
    /// </summary>
    Dot,

    /// <summary>
    /// A semicolon.
    /// </summary>
    Semicolon,

    /// <summary>
    /// Anything else: an operator, a number, a character the scan has no use for.
    /// </summary>
    Other,

    /// <summary>
    /// The end of the text.
    /// </summary>
    End,
}
