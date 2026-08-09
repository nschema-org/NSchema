using NSchema.Model;
using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax.Triggers;

/// <summary>
/// <c>CREATE TRIGGER name timing event [OR event]… [UPDATE OF columns] ON schema.table
/// [FOR EACH ROW|STATEMENT] [WHEN (expr)] action;</c>
/// </summary>
/// <param name="Name">The trigger name.</param>
/// <param name="Timing">The timing keyword.</param>
/// <param name="Events">The firing events.</param>
/// <param name="On">The table the trigger attaches to.</param>
/// <param name="Action">The trigger action: a function call or an inline body.</param>
/// <param name="UpdateOfColumns">The <c>UPDATE OF</c> columns, or <see langword="null"/> when absent.</param>
/// <param name="Level">The <c>FOR EACH</c> level (default <see cref="TriggerLevel.Statement"/>).</param>
/// <param name="When">The <c>WHEN</c> condition, or <see langword="null"/>.</param>
/// <param name="NotForReplication">Whether the trigger does not fire for a replication agent's writes.</param>
public sealed record CreateTriggerStatement(
    Identifier Name,
    TriggerTiming Timing,
    TriggerEvent Events,
    QualifiedName On,
    TriggerAction Action,
    IReadOnlyList<Identifier>? UpdateOfColumns = null,
    TriggerLevel Level = TriggerLevel.Statement,
    SqlText? When = null,
    bool NotForReplication = false
) : NsqlStatement
{
    /// <summary>
    /// The <c>CREATE</c> keyword token.
    /// </summary>
    public Token CreateKeyword { get; init; } = Token.Keyword(NsqlKeywords.Create);

    /// <summary>
    /// The <c>TRIGGER</c> keyword token.
    /// </summary>
    public Token TriggerKeyword { get; init; } = Token.Keyword(NsqlKeywords.Trigger);

    /// <summary>
    /// The verbatim span of the header (timing, events, <c>ON</c> table, <c>FOR EACH</c>, <c>WHEN</c>) — filled by the parser or a factory.
    /// </summary>
    public Token HeaderToken { get; init; } = Token.Missing;

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
            yield return CreateKeyword;
            yield return TriggerKeyword;
            yield return Name;
            yield return HeaderToken;
            yield return Action;
            yield return SemicolonToken;
        }
    }
}
