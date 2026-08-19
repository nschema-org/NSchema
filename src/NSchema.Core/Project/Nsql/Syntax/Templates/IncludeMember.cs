using NSchema.Project.Nsql.Syntax.Tables;
using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax.Templates;

/// <summary>
/// An <c>INCLUDE template</c> member: merges a table template's members at this position.
/// </summary>
/// <param name="TemplateName">The included table template's name.</param>
public sealed record IncludeMember(Identifier TemplateName) : TableMember
{
    /// <summary>
    /// The <c>INCLUDE</c> keyword token.
    /// </summary>
    public Token IncludeKeyword { get; init; } = Token.Keyword(NsqlKeywords.Include);

    internal override IEnumerable<NsqlChild> Children
    {
        get
        {
            if (DocComment is { } doc)
            {
                yield return doc;
            }
            yield return IncludeKeyword;
            yield return TemplateName;
        }
    }
}
