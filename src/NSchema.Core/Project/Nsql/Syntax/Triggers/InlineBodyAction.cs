using NSchema.Model;
using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax.Triggers;

/// <summary>
/// <c>AS $$ body $$</c> — an inline dollar-quoted trigger body.
/// </summary>
/// <param name="Body">The body text, delimiters stripped.</param>
public sealed record InlineBodyAction(SqlText Body) : TriggerAction
{
    /// <summary>
    /// The <c>AS</c> keyword token.
    /// </summary>
    public Token AsKeyword { get; init; } = Token.Keyword(NsqlKeywords.As);

    /// <summary>
    /// The dollar-quoted body token — filled by the parser or a factory (its delimiter is chosen from the body).
    /// </summary>
    public Token BodyToken { get; init; } = Token.Missing;

    internal override IEnumerable<NsqlChild> Children
    {
        get
        {
            yield return AsKeyword;
            yield return BodyToken;
        }
    }
}
