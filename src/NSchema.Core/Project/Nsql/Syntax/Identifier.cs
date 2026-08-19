using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax;

/// <summary>
/// A name as written in the source, casing preserved.
/// </summary>
/// <param name="Token">The identifier token (bare or quoted).</param>
public sealed record Identifier(Token Token) : NsqlNode
{
    /// <summary>
    /// The name's decoded text — quotes and escapes resolved.
    /// </summary>
    public string Value => Token.Text;

    internal override IEnumerable<NsqlChild> Children => [Token];

    /// <summary>
    /// Builds a synthetic identifier (no source, no trivia) carrying <paramref name="value"/>, quoted if it must be.
    /// </summary>
    public static Identifier Synthetic(string value) =>
        new(NeedsQuoting(value)
            ? Token.QuotedIdentifier(value)
            : new Token(TokenKind.Identifier, value, SourcePosition.None));

    /// <summary>
    /// Whether <paramref name="value"/> cannot be written bare (empty, not an identifier, or a member opener).
    /// </summary>
    internal static bool NeedsQuoting(string value) =>
        value.Length == 0
        || !NsqlLexer.IsIdentifierStart(value[0])
        || NsqlKeywords.MemberOpeners.Contains(value)
        || !value.All(NsqlLexer.IsIdentifierPart);

    /// <inheritdoc/>
    public override string ToString() => Value;
}
