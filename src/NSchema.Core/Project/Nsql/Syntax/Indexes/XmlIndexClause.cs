using NSchema.Model.Indexes;
using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax.Indexes;

/// <summary>
/// The XML facet of a <c>CREATE INDEX</c>. A primary is written <c>PRIMARY XML</c> before <c>INDEX</c> and carries
/// nothing after the keys; a secondary is written <c>XML</c> before <c>INDEX</c> and
/// <c>USING XML INDEX primary FOR {PATH|VALUE|PROPERTY}</c> after them — so only a secondary prints as a node,
/// while the leading keywords ride the statement.
/// </summary>
/// <param name="Kind">Which form of the node table this index is.</param>
/// <param name="PrimaryIndex">The primary XML index a secondary is built over; <see langword="null"/> for a primary.</param>
public sealed record XmlIndexClause(XmlIndexKind Kind, Identifier? PrimaryIndex = null) : NsqlNode
{
    /// <summary>
    /// Whether this is the node table itself rather than an index over one.
    /// </summary>
    public bool IsPrimary => Kind == XmlIndexKind.Primary;

    /// <summary>
    /// The <c>USING</c> keyword token, on a secondary.
    /// </summary>
    public Token? UsingKeyword { get; init; }

    /// <summary>
    /// The <c>XML</c> keyword token of the <c>USING XML INDEX</c> clause, on a secondary.
    /// </summary>
    public Token? XmlKeyword { get; init; }

    /// <summary>
    /// The <c>INDEX</c> keyword token of the <c>USING XML INDEX</c> clause, on a secondary.
    /// </summary>
    public Token? IndexKeyword { get; init; }

    /// <summary>
    /// The <c>FOR</c> keyword token, on a secondary.
    /// </summary>
    public Token? ForKeyword { get; init; }

    /// <summary>
    /// The <c>PATH</c>/<c>VALUE</c>/<c>PROPERTY</c> token, on a secondary.
    /// </summary>
    public Token? KindToken { get; init; }

    /// <summary>
    /// The keyword naming <paramref name="kind"/>.
    /// </summary>
    public static string KeywordFor(XmlIndexKind kind) => kind switch
    {
        XmlIndexKind.Path => NsqlKeywords.Path,
        XmlIndexKind.Value => NsqlKeywords.Value,
        XmlIndexKind.Property => NsqlKeywords.Property,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "A primary XML index has no FOR clause."),
    };

    internal override IEnumerable<NsqlChild> Children
    {
        get
        {
            if (IsPrimary)
            {
                yield break;
            }
            yield return UsingKeyword ?? Token.Keyword(NsqlKeywords.Using);
            yield return XmlKeyword ?? Token.Keyword(NsqlKeywords.Xml);
            yield return IndexKeyword ?? Token.Keyword(NsqlKeywords.Index);
            yield return PrimaryIndex!;
            yield return ForKeyword ?? Token.Keyword(NsqlKeywords.For);
            yield return KindToken ?? Token.Keyword(KeywordFor(Kind));
        }
    }
}
