using System.Collections;
using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax;

/// <summary>
/// A parenthesised list of column names, <c>(a, b, …)</c> — the parentheses plus the separated identifiers.
/// Behaves as the list of columns for consumers, and reprints exactly for the printer.
/// </summary>
/// <param name="Columns">The column identifiers with their separators.</param>
public sealed record ColumnList(SeparatedSyntaxList<Identifier> Columns) : NsqlNode, IReadOnlyList<Identifier>
{
    /// <summary>
    /// The <c>(</c> token.
    /// </summary>
    public Token OpenParenToken { get; init; } = Token.Punctuation(TokenKind.LeftParen, NsqlSymbols.LeftParen);

    /// <summary>
    /// The <c>)</c> token.
    /// </summary>
    public Token CloseParenToken { get; init; } = Token.Punctuation(TokenKind.RightParen, NsqlSymbols.RightParen);

    /// <inheritdoc/>
    public int Count => Columns.Count;

    /// <inheritdoc/>
    public Identifier this[int index] => Columns[index];

    /// <inheritdoc/>
    public IEnumerator<Identifier> GetEnumerator() => Columns.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    internal override IEnumerable<NsqlChild> Children
    {
        get
        {
            yield return OpenParenToken;
            foreach (var child in Columns.Children)
            {
                yield return child;
            }
            yield return CloseParenToken;
        }
    }

    /// <summary>
    /// Builds a synthetic column list (no source) from <paramref name="columns"/>.
    /// </summary>
    public static ColumnList Synthetic(IReadOnlyList<Identifier> columns) =>
        new(new SeparatedSyntaxList<Identifier>(columns));
}
