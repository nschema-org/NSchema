using NSchema.Model;
using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax;

/// <summary>
/// <c>RENAME &lt;kind&gt; schema.name TO name;</c> for a schema-level object.
/// </summary>
/// <param name="Kind">The kind of object being renamed.</param>
/// <param name="From">The object's current address.</param>
/// <param name="To">The name the object is renamed to.</param>
public sealed record RenameObjectStatement(SchemaObjectKind Kind, QualifiedName From, Identifier To) : NsqlStatement
{
    /// <summary>
    /// The <c>RENAME</c> keyword token.
    /// </summary>
    public Token RenameKeyword { get; init; } = Token.Keyword(NsqlKeywords.Rename);

    /// <summary>
    /// The keyword token(s) naming the kind (one, or <c>MATERIALIZED VIEW</c>), when parsed.
    /// </summary>
    public IReadOnlyList<Token> KindKeywords { get; init; } = [];

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
            yield return RenameKeyword;
            foreach (var keyword in KindKeywords)
            {
                yield return keyword;
            }
            yield return From;
            yield return ToKeyword;
            yield return To;
            yield return SemicolonToken;
        }
    }
}
