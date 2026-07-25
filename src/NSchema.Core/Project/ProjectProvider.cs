using Microsoft.Extensions.FileSystemGlobbing;
using NSchema.Model;
using NSchema.Project.Domain.Directives;
using NSchema.Project.Nsql;

namespace NSchema.Project;

/// <summary>
/// The default <see cref="IProjectProvider"/>.
/// </summary>
internal sealed class ProjectProvider(IEnumerable<ProjectSource> sources) : IProjectProvider
{
    public async ValueTask<Result<ProjectDefinition>> GetProject(PlanningScope scope, CancellationToken cancellationToken = default)
    {
        var sourceList = sources.ToList();
        if (sourceList.Count == 0)
        {
            throw new InvalidOperationException("No SQL schema sources are registered.");
        }

        // Each source contributes its matched files in sorted order; sources stay in registration order (so an
        // environment overlay layers after the base). Reads fan out, then combine in that deterministic order so
        // duplicate detection and script ordering are stable.
        var filePaths = sourceList.SelectMany(ResolveFiles).ToList();
        if (filePaths.Count == 0)
        {
            return Result.Failure<ProjectDefinition>(ProjectDiagnostics.NoFilesMatched());
        }

        var files = await Task.WhenAll(filePaths.Select(file => NsqlReader.ReadFile(file, cancellationToken)));

        // Unreadable or unparseable files and cross-file duplicates are authoring mistakes — error diagnostics
        // on the result, all of them at once, with the best-effort merge of the readable files still carried.
        var diagnostics = new DiagnosticCollector();
        var documents = new List<NsqlDocument>();
        foreach (var file in files)
        {
            diagnostics.Add(file);
            if (file.IsSuccess)
            {
                documents.Add(file.Value);
            }
        }

        var assembleResult = ProjectAssembler.Assemble(documents);
        var project = diagnostics.Require(assembleResult);

        var scopedProject = project.ScopedTo(scope);
        return diagnostics.ToResult(scopedProject);
    }

    private static IEnumerable<string> ResolveFiles(ProjectSource source) => source.Matcher
        .GetResultsInFullPath(source.BaseDirectory)
        .OrderBy(path => path, StringComparer.Ordinal);
}
