using NSchema.Model.Tables;
using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax.Tables;

/// <summary>
/// <c>REPLICA IDENTITY {FULL | NOTHING | USING INDEX name}</c> after a table's body.
/// </summary>
/// <param name="Kind">How a row is identified.</param>
/// <param name="Index">The identifying index, for <see cref="ReplicaIdentityKind.Index"/>.</param>
public sealed record ReplicaIdentityClause(ReplicaIdentityKind Kind, Identifier? Index = null) : NsqlNode
{
    /// <summary>
    /// The keyword tokens, when parsed: <c>REPLICA IDENTITY</c> followed by <c>FULL</c>, <c>NOTHING</c> or <c>USING INDEX</c>.
    /// </summary>
    public IReadOnlyList<Token> Keywords { get; init; } = [];

    internal override IEnumerable<NsqlChild> Children
    {
        get
        {
            var keywords = Keywords.Count > 0
                ? Keywords
                : [
                    Token.Keyword(NsqlKeywords.Replica),
                    Token.Keyword(NsqlKeywords.Identity),
                    .. Kind switch
                    {
                        ReplicaIdentityKind.Full => [Token.Keyword(NsqlKeywords.Full)],
                        ReplicaIdentityKind.Nothing => [Token.Keyword(NsqlKeywords.Nothing)],
                        _ => (Token[])[Token.Keyword(NsqlKeywords.Using), Token.Keyword(NsqlKeywords.Index)],
                    },
                ];
            foreach (var keyword in keywords)
            {
                yield return keyword;
            }
            if (Index is not null)
            {
                yield return Index;
            }
        }
    }
}
