using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax.Tables;

/// <summary>
/// <c>GRANT privilege[, privilege]… ON schema.table TO role;</c>
/// </summary>
/// <param name="Privileges">The granted privileges as written.</param>
/// <param name="On">The table granted on.</param>
/// <param name="Role">The role granted to.</param>
public sealed record GrantTableStatement(SeparatedSyntaxList<Privilege> Privileges, QualifiedName On, Identifier Role) : NsqlStatement
{
    /// <summary>The granted privileges folded into a flag set.</summary>
    public TablePrivilege PrivilegeFlags
    {
        get
        {
            var flags = TablePrivilege.None;
            foreach (var privilege in Privileges)
            {
                flags |= privilege.Value;
            }
            return flags;
        }
    }

    /// <summary>
    /// The <c>GRANT</c> keyword token.
    /// </summary>
    public Token GrantKeyword { get; init; } = Token.Keyword(NsqlKeywords.Grant);

    /// <summary>
    /// The <c>ON</c> keyword token.
    /// </summary>
    public Token OnKeyword { get; init; } = Token.Keyword(NsqlKeywords.On);

    /// <summary>
    /// The <c>TO</c> keyword token.
    /// </summary>
    public Token ToKeyword { get; init; } = Token.Keyword(NsqlKeywords.To);

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
            yield return GrantKeyword;
            foreach (var child in Privileges.Children)
            {
                yield return child;
            }
            yield return OnKeyword;
            yield return On;
            yield return ToKeyword;
            yield return Role;
            yield return SemicolonToken;
        }
    }
}
