using NSchema.Project.Nsql.Syntax.Tables;
using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax.Constraints;

/// <summary>
/// <c>CONSTRAINT name UNIQUE [CLUSTERED|NONCLUSTERED] (columns)</c>.
/// </summary>
/// <param name="Name">The constraint name.</param>
/// <param name="Columns">The unique columns.</param>
/// <param name="Clustered">Whether the backing index was written <c>CLUSTERED</c> or <c>NONCLUSTERED</c>.</param>
public sealed record UniqueDefinition(Identifier Name, ColumnList Columns, bool? Clustered = null) : TableMember
{
    /// <summary>
    /// The <c>CONSTRAINT</c> keyword token.
    /// </summary>
    public Token ConstraintKeyword { get; init; } = Token.Keyword(NsqlKeywords.Constraint);

    /// <summary>
    /// The <c>UNIQUE</c> keyword token.
    /// </summary>
    public Token UniqueKeyword { get; init; } = Token.Keyword(NsqlKeywords.Unique);

    /// <summary>
    /// The <c>CLUSTERED</c> or <c>NONCLUSTERED</c> keyword token, when either was written.
    /// </summary>
    public Token? ClusteredKeyword { get; init; }

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
            yield return UniqueKeyword;
            if (Clustered is { } clustered)
            {
                yield return ClusteredKeyword ?? Token.Keyword(clustered ? NsqlKeywords.Clustered : NsqlKeywords.Nonclustered);
            }
            yield return Columns;
        }
    }
}
