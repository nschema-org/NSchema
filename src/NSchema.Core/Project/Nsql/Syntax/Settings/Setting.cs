using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax.Settings;

/// <summary>
/// A single <c>key = value</c> setting; the key may be dotted (<c>pool.max</c>).
/// </summary>
/// <param name="Key">The attribute key as written.</param>
/// <param name="Value">The attribute value as written; the binder converts it to the target type.</param>
public sealed record Setting(string Key, string Value) : NsqlNode
{
    /// <summary>
    /// The verbatim (possibly dotted) key span token.
    /// </summary>
    public Token KeyToken { get; init; } = Token.Span(Key);

    /// <summary>
    /// The <c>=</c> token.
    /// </summary>
    public Token EqualsToken { get; init; } = Token.Punctuation(TokenKind.Equals, NsqlSymbols.Equal);

    /// <summary>
    /// The verbatim value span token.
    /// </summary>
    public Token ValueToken { get; init; } = Token.StringLiteral(Value);

    internal override IEnumerable<NsqlChild> Children
    {
        get
        {
            yield return KeyToken;
            yield return EqualsToken;
            yield return ValueToken;
        }
    }
}
