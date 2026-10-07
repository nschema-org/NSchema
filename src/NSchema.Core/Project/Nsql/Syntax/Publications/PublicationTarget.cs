using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax.Publications;

/// <summary>
/// One item of a publication's <c>FOR</c> list: a table, or a schema's tables.
/// </summary>
public abstract record PublicationTarget : NsqlNode
{
    /// <summary>
    /// The keywords opening a run of items of this kind (<c>TABLE</c>, or <c>TABLES IN SCHEMA</c>); empty when
    /// the item continues the run before it.
    /// </summary>
    public IReadOnlyList<Token> KindKeywords { get; init; } = [];
}
