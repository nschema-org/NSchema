using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax;

/// <summary>
/// An optionally schema-qualified name as written. Inside a template body an unqualified name binds to
/// the applied schema at projection; at the top level qualification is required by the grammar.
/// </summary>
/// <param name="Schema">The schema qualifier, or <see langword="null"/> when written unqualified.</param>
/// <param name="Name">The object name.</param>
public sealed record QualifiedName(Identifier? Schema, Identifier Name) : NsqlNode
{
    /// <summary>
    /// The <c>.</c> token between qualifier and name — present only when qualified.
    /// </summary>
    public Token DotToken { get; init; } = Token.Punctuation(TokenKind.Dot, NsqlSymbols.Dot);

    internal override IEnumerable<NsqlChild> Children
    {
        get
        {
            if (Schema != null)
            {
                yield return Schema;
                yield return DotToken;
            }
            yield return Name;
        }
    }
}
