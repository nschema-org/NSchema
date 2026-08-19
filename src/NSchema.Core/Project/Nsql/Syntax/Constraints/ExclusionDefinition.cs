using NSchema.Model;
using NSchema.Project.Nsql.Syntax.Tables;
using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax.Constraints;

/// <summary>
/// <c>CONSTRAINT name EXCLUDE [USING method] (element WITH operator, …) [WHERE (predicate)]</c>.
/// </summary>
/// <param name="Name">The constraint name.</param>
/// <param name="Elements">The exclusion elements.</param>
/// <param name="Method">The access method after <c>USING</c>, or <see langword="null"/>.</param>
/// <param name="Predicate">The partial-constraint predicate, or <see langword="null"/>.</param>
public sealed record ExclusionDefinition(
    Identifier Name,
    SeparatedSyntaxList<ExclusionElement> Elements,
    Identifier? Method = null,
    SqlText? Predicate = null
) : TableMember
{
    /// <summary>
    /// The <c>CONSTRAINT</c> keyword token.
    /// </summary>
    public Token ConstraintKeyword { get; init; } = Token.Keyword(NsqlKeywords.Constraint);

    /// <summary>
    /// The <c>EXCLUDE</c> keyword token.
    /// </summary>
    public Token ExcludeKeyword { get; init; } = Token.Keyword(NsqlKeywords.Exclude);

    /// <summary>
    /// The <c>USING</c> keyword token, when written with a method.
    /// </summary>
    public Token? UsingKeyword { get; init; }

    /// <summary>
    /// The <c>(</c> token opening the elements.
    /// </summary>
    public Token OpenParenToken { get; init; } = Token.Punctuation(TokenKind.LeftParen, NsqlSymbols.LeftParen);

    /// <summary>
    /// The <c>)</c> token closing the elements.
    /// </summary>
    public Token CloseParenToken { get; init; } = Token.Punctuation(TokenKind.RightParen, NsqlSymbols.RightParen);

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
            yield return ConstraintKeyword;
            yield return Name;
            yield return ExcludeKeyword;
            if (Method is { } method)
            {
                yield return UsingKeyword ?? Token.Keyword(NsqlKeywords.Using);
                yield return method;
            }
            yield return OpenParenToken;
            foreach (var child in Elements.Children)
            {
                yield return child;
            }
            yield return CloseParenToken;
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
