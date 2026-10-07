using NSchema.Model;
using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax.Publications;

/// <summary>
/// <c>[TABLE] schema.table [(column, …)] [WHERE (filter)]</c> in a publication's <c>FOR</c> list.
/// </summary>
/// <param name="Table">The published table.</param>
/// <param name="Columns">The published columns, or <see langword="null"/> for all of them.</param>
/// <param name="Filter">The row filter, or <see langword="null"/>.</param>
public sealed record PublishedTableTarget(QualifiedName Table, ColumnList? Columns = null, SqlText? Filter = null) : PublicationTarget
{
    /// <summary>
    /// The <c>WHERE</c> keyword token, when parsed with a filter.
    /// </summary>
    public Token? WhereKeyword { get; init; }

    /// <summary>
    /// The filter's <c>(</c> token, when parsed.
    /// </summary>
    public Token? WhereOpenParenToken { get; init; }

    /// <summary>
    /// The filter's verbatim span, when parsed.
    /// </summary>
    public Token? FilterToken { get; init; }

    /// <summary>
    /// The filter's <c>)</c> token, when parsed.
    /// </summary>
    public Token? WhereCloseParenToken { get; init; }

    internal override IEnumerable<NsqlChild> Children
    {
        get
        {
            foreach (var keyword in KindKeywords)
            {
                yield return keyword;
            }
            yield return Table;
            if (Columns is not null)
            {
                yield return Columns;
            }
            if (Filter is not null)
            {
                yield return WhereKeyword ?? Token.Keyword(NsqlKeywords.Where);
                yield return WhereOpenParenToken ?? Token.Punctuation(TokenKind.LeftParen, NsqlSymbols.LeftParen);
                yield return FilterToken ?? Token.Span(Filter.Value);
                yield return WhereCloseParenToken ?? Token.Punctuation(TokenKind.RightParen, NsqlSymbols.RightParen);
            }
        }
    }
}
