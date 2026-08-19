using NSchema.Model;
using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax.Scripts;

/// <summary>
/// <c>SCRIPT name RUN [ALWAYS|ONCE] ON event [(options)] AS $$ body $$;</c>
/// </summary>
/// <param name="Name">The script name.</param>
/// <param name="RunCondition">The <c>RUN</c> condition, or <see langword="null"/> for the bare <c>RUN</c> of a change-event script.</param>
/// <param name="Event">The <c>ON</c> event clause.</param>
/// <param name="Body">The body text, delimiters stripped.</param>
/// <param name="RunOutsideTransaction">Whether <c>run_outside_transaction = true</c> is written.</param>
public sealed record ScriptStatement(
    Identifier Name,
    RunCondition? RunCondition,
    ScriptEventClause Event,
    SqlText Body,
    bool RunOutsideTransaction = false
) : NsqlStatement
{
    /// <summary>
    /// The <c>RUN</c> condition.
    /// </summary>
    public bool HasMisplacedRunCondition => RunCondition is not null && Event is not DeploymentEventClause;

    /// <summary>
    /// The <c>SCRIPT</c> keyword token.
    /// </summary>
    public Token ScriptKeyword { get; init; } = Token.Keyword(NsqlKeywords.Script);

    /// <summary>
    /// The <c>RUN</c> keyword token.
    /// </summary>
    public Token RunKeyword { get; init; } = Token.Keyword(NsqlKeywords.Run);

    /// <summary>
    /// The <c>ALWAYS</c>/<c>ONCE</c> keyword token, when written with a condition.
    /// </summary>
    public Token? ConditionKeyword { get; init; }

    /// <summary>
    /// The <c>ON</c> keyword token.
    /// </summary>
    public Token OnKeyword { get; init; } = Token.Keyword(NsqlKeywords.On);

    /// <summary>
    /// The <c>(</c> token opening the options, when written with options.
    /// </summary>
    public Token? OptionsOpenParenToken { get; init; }

    /// <summary>
    /// The verbatim options-interior span token, when parsed with options.
    /// </summary>
    public Token? OptionsInteriorToken { get; init; }

    /// <summary>
    /// The <c>)</c> token closing the options, when written with options.
    /// </summary>
    public Token? OptionsCloseParenToken { get; init; }

    /// <summary>
    /// The <c>AS</c> keyword token.
    /// </summary>
    public Token AsKeyword { get; init; } = Token.Keyword(NsqlKeywords.As);

    /// <summary>
    /// The dollar-quoted body token — filled by the parser or a factory (its delimiter is chosen from the body).
    /// </summary>
    public Token BodyToken { get; init; } = Token.Missing;

    /// <summary>
    /// The terminating <c>;</c> token.
    /// </summary>
    public Token SemicolonToken { get; init; } = Token.Punctuation(TokenKind.Semicolon, NsqlSymbols.Semicolon);

    internal override IEnumerable<NsqlChild> Children
    {
        get
        {
            if (DocComment is { } doc)
            {
                yield return doc;
            }
            yield return ScriptKeyword;
            yield return Name;
            yield return RunKeyword;
            if (RunCondition is { } condition)
            {
                yield return ConditionKeyword ?? Token.Keyword(condition == Scripts.RunCondition.Once ? NsqlKeywords.Once : NsqlKeywords.Always);
            }
            yield return OnKeyword;
            yield return Event;
            if (RunOutsideTransaction)
            {
                yield return OptionsOpenParenToken ?? Token.Punctuation(TokenKind.LeftParen, NsqlSymbols.LeftParen);
                yield return OptionsInteriorToken ?? Token.Span("run_outside_transaction = true");
                yield return OptionsCloseParenToken ?? Token.Punctuation(TokenKind.RightParen, NsqlSymbols.RightParen);
            }
            yield return AsKeyword;
            yield return BodyToken;
            yield return SemicolonToken;
        }
    }
}
