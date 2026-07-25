using NSchema.Project.Domain.Directives;
using NSchema.Project.Nsql;

namespace NSchema.Tests.Helpers;

/// <summary>
/// The parse-and-assemble pipeline with the old parser's shape: construct with source, <see cref="Parse"/> to a
/// projected <see cref="ProjectDefinition"/>. Parse errors throw as <c>NsqlSyntaxException</c> (these tests
/// assert the raw errors); assembly findings ride as diagnostics on the result.
/// </summary>
internal sealed class TestNsqlParser(string source)
{
    public ProjectDefinition Parse() => Project().Require();

    /// <summary>
    /// The full projection result: parse errors still throw (the grammar tests assert the raw exceptions),
    /// while assembly findings — duplicates, unknown references, broken template bodies — ride as diagnostics.
    /// </summary>
    public Result<ProjectDefinition> Project()
    {
        var parser = new NsqlParser(source);
        var document = parser.Parse();
        if (parser.Errors.Count > 0)
        {
            throw parser.Errors[0];
        }

        return NSchema.Project.ProjectAssembler.Assemble([document]);
    }

    /// <summary>
    /// Assembles several sources as separate documents — the cross-file path. Each document is stamped with
    /// a synthetic file name (<c>file1.sql</c>, <c>file2.sql</c>, …) so findings can assert attribution.
    /// </summary>
    public static Result<ProjectDefinition> Assemble(params string[] sources)
    {
        var documents = sources.Select((text, index) =>
        {
            var parser = new NsqlParser(text);
            var document = parser.Parse();
            if (parser.Errors.Count > 0)
            {
                throw parser.Errors[0];
            }

            return document with { FilePath = $"file{index + 1}.sql" };
        }).ToList();

        return NSchema.Project.ProjectAssembler.Assemble(documents);
    }
}
