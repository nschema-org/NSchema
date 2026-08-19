using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax.Tables;

/// <summary>
/// <c>RENAME COLUMN schema.table.column TO name;</c>
/// </summary>
/// <param name="From">The column's current address.</param>
/// <param name="To">The name the column is renamed to.</param>
public sealed record RenameColumnStatement(MemberPath From, Identifier To) : NsqlStatement
{
    /// <summary>
    /// The <c>RENAME</c> keyword token.
    /// </summary>
    public Token RenameKeyword { get; init; } = Token.Keyword(NsqlKeywords.Rename);

    /// <summary>
    /// The <c>COLUMN</c> keyword token.
    /// </summary>
    public Token ColumnKeyword { get; init; } = Token.Keyword(NsqlKeywords.Column);

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
            yield return ColumnKeyword;
            yield return From;
            yield return ToKeyword;
            yield return To;
            yield return SemicolonToken;
        }
    }
}
