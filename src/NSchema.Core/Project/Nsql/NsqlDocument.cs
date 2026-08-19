using NSchema.Model;
using NSchema.Project.Nsql.Syntax;

namespace NSchema.Project.Nsql;

/// <summary>
/// A parsed NSchema project source file: the statements as written, in order. One document is one file.
/// </summary>
/// <param name="Statements">The top-level statements in source order.</param>
public sealed record NsqlDocument(IReadOnlyList<NsqlStatement> Statements) : NsqlSourceDocument
{
    /// <summary>
    /// A document with no statements.
    /// </summary>
    public static NsqlDocument Empty { get; } = new([]);

    /// <summary>
    /// The document that declares <paramref name="database"/> — the schema model as NSQL.
    /// </summary>
    /// <param name="database">The schema to declare.</param>
    /// <param name="declareSchemas">Whether to emit a <c>CREATE SCHEMA</c> for each schema.</param>
    public static NsqlDocument From(Database database, bool declareSchemas = true) =>
        SyntaxBuilder.Build(database, declareSchemas);

    /// <summary>
    /// One document holding <paramref name="documents"/>' statements end to end, in order — how a file assembled from
    /// several contributors is composed before it is written.
    /// </summary>
    /// <param name="documents">The documents to concatenate.</param>
    public static NsqlDocument Concat(params IEnumerable<NsqlDocument> documents) =>
        new([.. documents.SelectMany(document => document.Statements)]);

    private protected override IReadOnlyList<NsqlStatement> StatementNodes => Statements;
}
