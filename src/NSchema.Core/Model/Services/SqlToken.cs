namespace NSchema.Model.Services;

/// <summary>
/// One token from a <see cref="SqlLexer"/>.
/// </summary>
/// <param name="Kind">What it is.</param>
/// <param name="Value">A word's text, a quoted identifier's inner text (doubling undone), a dollar-quoted
/// block's inner SQL, or a string's raw text; empty for punctuation.</param>
/// <param name="Start">The character offset the token starts at.</param>
/// <param name="Length">The character length of the token as written.</param>
public readonly record struct SqlToken(SqlTokenKind Kind, string Value, int Start, int Length);
