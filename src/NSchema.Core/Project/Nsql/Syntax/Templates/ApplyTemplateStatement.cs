using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax.Templates;

/// <summary>
/// <c>APPLY TEMPLATE name IN SCHEMA a[, b]…;</c>
/// </summary>
/// <param name="TemplateName">The applied template's name.</param>
/// <param name="Schemas">The target schemas.</param>
public sealed record ApplyTemplateStatement(Identifier TemplateName, SeparatedSyntaxList<Identifier> Schemas) : NsqlStatement
{
    /// <summary>
    /// The <c>APPLY</c> keyword token.
    /// </summary>
    public Token ApplyKeyword { get; init; } = Token.Keyword(NsqlKeywords.Apply);

    /// <summary>
    /// The <c>TEMPLATE</c> keyword token.
    /// </summary>
    public Token TemplateKeyword { get; init; } = Token.Keyword(NsqlKeywords.Template);

    /// <summary>
    /// The <c>IN</c> keyword token.
    /// </summary>
    public Token InKeyword { get; init; } = Token.Keyword(NsqlKeywords.In);

    /// <summary>
    /// The <c>SCHEMA</c> keyword token.
    /// </summary>
    public Token SchemaKeyword { get; init; } = Token.Keyword(NsqlKeywords.Schema);

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
            yield return ApplyKeyword;
            yield return TemplateKeyword;
            yield return TemplateName;
            yield return InKeyword;
            yield return SchemaKeyword;
            foreach (var child in Schemas.Children)
            {
                yield return child;
            }
            yield return SemicolonToken;
        }
    }
}
