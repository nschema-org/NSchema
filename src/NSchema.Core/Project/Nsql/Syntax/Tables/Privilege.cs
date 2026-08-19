using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax.Tables;

/// <summary>
/// A single privilege in a table grant, as written (a <c>SELECT</c>/<c>INSERT</c>/<c>UPDATE</c>/<c>DELETE</c> keyword).
/// </summary>
/// <param name="Keyword">The privilege keyword token.</param>
public sealed record Privilege(Token Keyword) : NsqlNode
{
    /// <summary>
    /// The privilege this keyword names.
    /// </summary>
    public TablePrivilege Value =>
        Keyword.IsKeyword(NsqlKeywords.Select) ? TablePrivilege.Select
        : Keyword.IsKeyword(NsqlKeywords.Insert) ? TablePrivilege.Insert
        : Keyword.IsKeyword(NsqlKeywords.Update) ? TablePrivilege.Update
        : Keyword.IsKeyword(NsqlKeywords.Delete) ? TablePrivilege.Delete
        : TablePrivilege.None;

    internal override IEnumerable<NsqlChild> Children => [Keyword];

    /// <summary>
    /// Builds a synthetic privilege (no source) for <paramref name="privilege"/>.
    /// </summary>
    public static Privilege Synthetic(TablePrivilege privilege) =>
        new(new Token(TokenKind.Identifier, privilege.ToString().ToUpperInvariant(), SourcePosition.None));
}
