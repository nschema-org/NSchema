using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax;

/// <summary>
/// A type reference as written: an optionally schema-qualified type name plus its optional
/// parenthesised arguments (e.g. <c>varchar(100)</c>, <c>numeric(10,2)</c>, <c>app.status</c>).
/// </summary>
/// <param name="Schema">The schema qualifier for a user-defined type, or <see langword="null"/>.</param>
/// <param name="Name">The type name.</param>
/// <param name="Arguments">The text inside the parentheses (e.g. <c>100</c> or <c>10,2</c>), or <see langword="null"/>.</param>
/// <param name="XmlCollection">
/// The XML schema collection a typed <c>xml</c> reads <c>xml(CONTENT s.c)</c>; <see langword="null"/> otherwise.
/// It occupies the argument position but names an object rather than a number, so it is its own component.
/// </param>
/// <param name="IsDocument">Whether a typed <c>xml</c> was written <c>DOCUMENT</c> rather than <c>CONTENT</c>.</param>
public sealed record TypeName(
    Identifier? Schema,
    Identifier Name,
    string? Arguments = null,
    QualifiedName? XmlCollection = null,
    bool IsDocument = false
) : NsqlNode
{
    /// <summary>
    /// The <c>CONTENT</c>/<c>DOCUMENT</c> keyword token, when parsed as a typed <c>xml</c>.
    /// </summary>
    public Token? ContentKeyword { get; init; }

    /// <summary>
    /// The <c>.</c> token after the schema qualifier, when parsed qualified.
    /// </summary>
    public Token? SchemaDotToken { get; init; }

    /// <summary>
    /// The <c>(</c> token opening the arguments, when parsed with arguments.
    /// </summary>
    public Token? OpenParenToken { get; init; }

    /// <summary>
    /// The precision (first) argument token, when parsed with arguments.
    /// </summary>
    public Token? PrecisionToken { get; init; }

    /// <summary>
    /// The <c>,</c> token between precision and scale, when parsed with a scale.
    /// </summary>
    public Token? CommaToken { get; init; }

    /// <summary>The scale (second) argument token, when parsed with a scale.</summary>
    public Token? ScaleToken { get; init; }

    /// <summary>
    /// The <c>)</c> token closing the arguments, when parsed with arguments.
    /// </summary>
    public Token? CloseParenToken { get; init; }

    internal override IEnumerable<NsqlChild> Children
    {
        get
        {
            if (Schema != null)
            {
                yield return Schema;
                yield return SchemaDotToken ?? Token.Punctuation(TokenKind.Dot, NsqlSymbols.Dot);
            }
            yield return Name;
            if (XmlCollection != null)
            {
                yield return OpenParenToken ?? Token.Punctuation(TokenKind.LeftParen, NsqlSymbols.LeftParen);
                yield return ContentKeyword ?? Token.Keyword(IsDocument ? NsqlKeywords.Document : NsqlKeywords.Content);
                yield return XmlCollection;
                yield return CloseParenToken ?? Token.Punctuation(TokenKind.RightParen, NsqlSymbols.RightParen);
                yield break;
            }
            if (Arguments is not null)
            {
                yield return OpenParenToken ?? Token.Punctuation(TokenKind.LeftParen, NsqlSymbols.LeftParen);
                if (PrecisionToken is { } precision)
                {
                    yield return precision;
                    if (CommaToken is { } comma)
                    {
                        yield return comma;
                    }
                    if (ScaleToken is { } scale)
                    {
                        yield return scale;
                    }
                }
                else
                {
                    // A synthetic type carries its arguments as one verbatim span; the parser re-splits them.
                    yield return Token.Span(Arguments);
                }
                yield return CloseParenToken ?? Token.Punctuation(TokenKind.RightParen, NsqlSymbols.RightParen);
            }
        }
    }
}
