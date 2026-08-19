using NSchema.Model;
using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax.XmlSchemaCollections;

/// <summary>
/// <c>CREATE XML SCHEMA COLLECTION schema.name AS body.</c> The body is the collected XSD, opaque like a view's.
/// </summary>
/// <param name="Name">The collection name as written.</param>
/// <param name="Body">The XSD the collection holds, verbatim.</param>
public sealed record CreateXmlSchemaCollectionStatement(QualifiedName Name, SqlText Body) : NsqlStatement
{
    /// <summary>
    /// The <c>CREATE</c> keyword token.
    /// </summary>
    public Token CreateKeyword { get; init; } = Token.Keyword(NsqlKeywords.Create);

    /// <summary>
    /// The <c>XML</c> keyword token.
    /// </summary>
    public Token XmlKeyword { get; init; } = Token.Keyword(NsqlKeywords.Xml);

    /// <summary>
    /// The <c>SCHEMA</c> keyword token.
    /// </summary>
    public Token SchemaKeyword { get; init; } = Token.Keyword(NsqlKeywords.Schema);

    /// <summary>
    /// The <c>COLLECTION</c> keyword token.
    /// </summary>
    public Token CollectionKeyword { get; init; } = Token.Keyword(NsqlKeywords.Collection);

    /// <summary>
    /// The <c>AS</c> keyword token.
    /// </summary>
    public Token AsKeyword { get; init; } = Token.Keyword(NsqlKeywords.As);

    /// <summary>
    /// The verbatim body span token.
    /// </summary>
    public Token BodyToken { get; init; } = Token.Span(Body.Value);

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
            yield return CreateKeyword;
            yield return XmlKeyword;
            yield return SchemaKeyword;
            yield return CollectionKeyword;
            yield return Name;
            yield return AsKeyword;
            yield return BodyToken;
            yield return SemicolonToken;
        }
    }
}
