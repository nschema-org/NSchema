using System.Collections;
using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax;

/// <summary>
/// A comma-separated list of syntax nodes: the element nodes plus the separator tokens between them,
/// A synthetic list carries elements only (no separator tokens); the formatter supplies them.
/// </summary>
/// <typeparam name="T">The element node type.</typeparam>
public readonly struct SeparatedSyntaxList<T> : IReadOnlyList<T> where T : NsqlNode
{
    /// <summary>
    /// Builds a list from its elements and the separator tokens between them.
    /// </summary>
    public SeparatedSyntaxList(IReadOnlyList<T> elements, IReadOnlyList<Token> separators)
    {
        Elements = elements;
        Separators = separators;
    }

    /// <summary>
    /// Builds a synthetic list (elements only, no separator tokens).
    /// </summary>
    public SeparatedSyntaxList(IReadOnlyList<T> elements) : this(elements, [])
    {
    }

    private IReadOnlyList<T> Elements => field ?? [];

    /// <summary>
    /// The separator tokens between the elements (the commas), for the formatter.
    /// </summary>
    public IReadOnlyList<Token> Separators => field ?? [];

    /// <inheritdoc/>
    public int Count => Elements.Count;

    /// <inheritdoc/>
    public T this[int index] => Elements[index];

    /// <inheritdoc/>
    public IEnumerator<T> GetEnumerator() => Elements.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// The elements and separator tokens in source order, for the printer.
    /// </summary>
    internal IEnumerable<NsqlChild> Children
    {
        get
        {
            var elements = Elements;
            var separators = Separators;
            for (var i = 0; i < elements.Count; i++)
            {
                yield return elements[i];
                if (i < elements.Count - 1)
                {
                    // A synthetic list has no separator tokens; supply a comma so the printer keeps the list valid.
                    yield return i < separators.Count ? separators[i] : Token.Punctuation(TokenKind.Comma, NsqlSymbols.Comma);
                }
            }
        }
    }
}
