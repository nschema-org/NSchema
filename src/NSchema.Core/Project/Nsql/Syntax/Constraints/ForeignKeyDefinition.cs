using NSchema.Project.Nsql.Syntax.Tables;
using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax.Constraints;

/// <summary>
/// <c>CONSTRAINT name FOREIGN KEY (columns) REFERENCES schema.table (columns) [ON DELETE action] [ON UPDATE action]</c>.
/// </summary>
/// <param name="Name">The constraint name.</param>
/// <param name="Columns">The referencing columns.</param>
/// <param name="References">The referenced table as written.</param>
/// <param name="ReferencedColumns">The referenced columns.</param>
/// <param name="OnDelete">The <c>ON DELETE</c> action (default <see cref="ReferentialAction.NoAction"/>).</param>
/// <param name="OnUpdate">The <c>ON UPDATE</c> action (default <see cref="ReferentialAction.NoAction"/>).</param>
public sealed record ForeignKeyDefinition(
    Identifier Name,
    ColumnList Columns,
    QualifiedName References,
    ColumnList ReferencedColumns,
    ReferentialAction OnDelete = ReferentialAction.NoAction,
    ReferentialAction OnUpdate = ReferentialAction.NoAction
) : TableMember
{
    /// <summary>
    /// The <c>CONSTRAINT</c> keyword token.
    /// </summary>
    public Token ConstraintKeyword { get; init; } = Token.Keyword(NsqlKeywords.Constraint);

    /// <summary>
    /// The <c>FOREIGN</c> keyword token.
    /// </summary>
    public Token ForeignKeyword { get; init; } = Token.Keyword(NsqlKeywords.Foreign);

    /// <summary>
    /// The <c>KEY</c> keyword token.
    /// </summary>
    public Token KeyKeyword { get; init; } = Token.Keyword(NsqlKeywords.Key);

    /// <summary>
    /// The <c>REFERENCES</c> keyword token.
    /// </summary>
    public Token ReferencesKeyword { get; init; } = Token.Keyword(NsqlKeywords.References);

    /// <summary>
    /// The verbatim span of the <c>ON DELETE</c>/<c>ON UPDATE</c> actions, when present.
    /// </summary>
    public Token? ActionsToken { get; init; }

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
            yield return ForeignKeyword;
            yield return KeyKeyword;
            yield return Columns;
            yield return ReferencesKeyword;
            yield return References;
            yield return ReferencedColumns;
            if (ActionsToken is { } actions)
            {
                yield return actions;
            }
        }
    }
}
