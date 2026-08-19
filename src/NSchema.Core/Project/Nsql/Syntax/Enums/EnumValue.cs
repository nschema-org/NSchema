using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax.Enums;

/// <summary>
/// A single value of an enum declaration, as written (a quoted string literal).
/// </summary>
/// <param name="Token">The string-literal token.</param>
public sealed record EnumValue(Token Token) : NsqlNode
{
    /// <summary>
    /// The value's decoded text (quotes and escapes resolved).
    /// </summary>
    public string Value => Token.Text;

    internal override IEnumerable<NsqlChild> Children => [Token];

    /// <summary>
    /// Builds a synthetic enum value (no source, no trivia) carrying <paramref name="value"/>.
    /// </summary>
    public static EnumValue Synthetic(string value) => new(Token.StringLiteral(value));
}
