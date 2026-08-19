using NSchema.Model;
using NSchema.Project.Nsql.Syntax.Tables;
using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax.Constraints;

/// <summary>
/// <c>CONSTRAINT name CHECK (expression)</c> — in a table body or a domain declaration.
/// </summary>
/// <param name="Name">The constraint name.</param>
/// <param name="Expression">The boolean expression the constraint enforces.</param>
public sealed record CheckDefinition(Identifier Name, SqlText Expression) : TableMember
{
    /// <summary>
    /// The <c>CONSTRAINT</c> keyword token.
    /// </summary>
    public Token ConstraintKeyword { get; init; } = Token.Keyword(NsqlKeywords.Constraint);

    /// <summary>
    /// The <c>CHECK</c> keyword token.
    /// </summary>
    public Token CheckKeyword { get; init; } = Token.Keyword(NsqlKeywords.Check);

    /// <summary>
    /// The <c>(</c> token.
    /// </summary>
    public Token OpenParenToken { get; init; } = Token.Punctuation(TokenKind.LeftParen, NsqlSymbols.LeftParen);

    /// <summary>
    /// The verbatim check-expression span token.
    /// </summary>
    public Token ExpressionToken { get; init; } = Token.Span(Expression.Value);

    /// <summary>
    /// The <c>)</c> token.
    /// </summary>
    public Token CloseParenToken { get; init; } = Token.Punctuation(TokenKind.RightParen, NsqlSymbols.RightParen);

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
            yield return CheckKeyword;
            yield return OpenParenToken;
            yield return ExpressionToken;
            yield return CloseParenToken;
        }
    }
}
