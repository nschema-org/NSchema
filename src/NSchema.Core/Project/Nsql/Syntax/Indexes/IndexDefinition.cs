using NSchema.Model;
using NSchema.Project.Nsql.Syntax.Tables;
using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax.Indexes;

/// <summary>
/// An inline index member: <c>[UNIQUE] [CLUSTERED|NONCLUSTERED] INDEX name [USING method] (keys) [INCLUDE (columns)] [WHERE (predicate)]</c>.
/// </summary>
/// <param name="Name">The index name.</param>
/// <param name="IsUnique">Whether the index is declared <c>UNIQUE</c>.</param>
/// <param name="Columns">The index keys.</param>
/// <param name="Method">The access method after <c>USING</c>, or <see langword="null"/>.</param>
/// <param name="Include">The <c>INCLUDE</c> columns, or <see langword="null"/> when absent.</param>
/// <param name="Predicate">The partial-index predicate, or <see langword="null"/>.</param>
/// <param name="Clustered">Whether <c>CLUSTERED</c> or <c>NONCLUSTERED</c> was written.</param>
public sealed record IndexDefinition(
    Identifier Name,
    bool IsUnique,
    SeparatedSyntaxList<IndexElement> Columns,
    Identifier? Method = null,
    ColumnList? Include = null,
    SqlText? Predicate = null,
    bool? Clustered = null
) : TableMember
{
    /// <summary>
    /// The <c>UNIQUE</c> keyword token, when written unique.
    /// </summary>
    public Token? UniqueKeyword { get; init; }

    /// <summary>
    /// The <c>CLUSTERED</c> or <c>NONCLUSTERED</c> keyword token, when either was written.
    /// </summary>
    public Token? ClusteredKeyword { get; init; }

    /// <summary>
    /// The <c>INDEX</c> keyword token.
    /// </summary>
    public Token IndexKeyword { get; init; } = Token.Keyword(NsqlKeywords.Index);

    /// <summary>
    /// The <c>USING</c> keyword token, when written with a method.
    /// </summary>
    public Token? UsingKeyword { get; init; }

    /// <summary>
    /// The <c>(</c> token opening the keys.
    /// </summary>
    public Token OpenParenToken { get; init; } = Token.Punctuation(TokenKind.LeftParen, NsqlSymbols.LeftParen);

    /// <summary>
    /// The <c>)</c> token closing the keys.
    /// </summary>
    public Token CloseParenToken { get; init; } = Token.Punctuation(TokenKind.RightParen, NsqlSymbols.RightParen);

    /// <summary>
    /// The <c>INCLUDE</c> keyword token, when written with included columns.
    /// </summary>
    public Token? IncludeKeyword { get; init; }

    /// <summary>
    /// The <c>WHERE</c> keyword token, when written with a predicate.
    /// </summary>
    public Token? WhereKeyword { get; init; }

    /// <summary>
    /// The <c>(</c> token opening the predicate, when written with a predicate.
    /// </summary>
    public Token? WhereOpenParenToken { get; init; }

    /// <summary>
    /// The verbatim predicate span token, when parsed with a predicate.
    /// </summary>
    public Token? PredicateToken { get; init; }

    /// <summary>
    /// The <c>)</c> token closing the predicate, when written with a predicate.
    /// </summary>
    public Token? WhereCloseParenToken { get; init; }

    internal override IEnumerable<NsqlChild> Children
    {
        get
        {
            if (DocComment is { } doc)
            {
                yield return doc;
            }
            if (IsUnique)
            {
                yield return UniqueKeyword ?? Token.Keyword(NsqlKeywords.Unique);
            }
            if (Clustered is { } clustered)
            {
                yield return ClusteredKeyword ?? Token.Keyword(clustered ? NsqlKeywords.Clustered : NsqlKeywords.Nonclustered);
            }
            yield return IndexKeyword;
            yield return Name;
            if (Method is { } method)
            {
                yield return UsingKeyword ?? Token.Keyword(NsqlKeywords.Using);
                yield return method;
            }
            yield return OpenParenToken;
            foreach (var child in Columns.Children)
            {
                yield return child;
            }
            yield return CloseParenToken;
            if (Include is { } include)
            {
                yield return IncludeKeyword ?? Token.Keyword(NsqlKeywords.Include);
                yield return include;
            }
            if (Predicate is { } predicate)
            {
                yield return WhereKeyword ?? Token.Keyword(NsqlKeywords.Where);
                yield return WhereOpenParenToken ?? Token.Punctuation(TokenKind.LeftParen, NsqlSymbols.LeftParen);
                yield return PredicateToken ?? Token.Span(predicate.Value);
                yield return WhereCloseParenToken ?? Token.Punctuation(TokenKind.RightParen, NsqlSymbols.RightParen);
            }
        }
    }
}
