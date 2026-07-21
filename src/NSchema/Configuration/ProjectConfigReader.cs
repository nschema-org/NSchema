using NSchema.Config;
using NSchema.Plugins.Model;
using NSchema.Configuration.Plugins;
using NSchema.Configuration.State;
using NSchema.Project.Nsql;
using NSchema.Project.Nsql.Syntax.Config;

namespace NSchema.Configuration;

/// <summary>
/// Reads project configuration from the <c>*.env.sql</c> (and selected <c>*.env.&lt;env&gt;.sql</c>) files under a directory.
/// </summary>
internal static class ProjectConfigReader
{
    /// <summary>
    /// Reads the project configuration, layering the selected <paramref name="environment"/>'s overlay (if any).
    /// </summary>
    /// <param name="root">The project directory.</param>
    /// <param name="environment">The target environment, or <see langword="null"/> for the base configuration only.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    public static async ValueTask<ProjectConfig> Read(string root, string? environment, CancellationToken cancellationToken = default)
    {
        var documents = await ReadLayer(ProjectGlobs.Match(root, ProjectGlobs.BaseConfiguration()), cancellationToken);

        if (environment is not null)
        {
            var overlayFiles = ProjectGlobs.Match(root, ProjectGlobs.EnvironmentConfiguration(environment));
            if (overlayFiles.Count == 0)
            {
                throw new InvalidOperationException($"No configuration files found for environment '{environment}'.");
            }

            var overlay = await ReadLayer(overlayFiles, cancellationToken);
            documents = Merge(documents, overlay);
        }

        var assembled = ConfigAssembler.Assemble(documents);
        ThrowOnErrors(assembled.Diagnostics);
        var definition = assembled.Require();

        ValidateEngineRequirement(definition.Engine);

        return new ProjectConfig
        {
            Provider = definition.Database is { } database ? PluginReference.Resolve(database, definition.Plugins) : null,
            State = definition.State is { } state ? StateConfig.Resolve(state, definition.Plugins) : null,
        };
    }

    private static async ValueTask<List<NsqlConfigDocument>> ReadLayer(IReadOnlyList<string> files, CancellationToken cancellationToken)
    {
        var diagnostics = new List<NsqlDiagnostic>();
        var documents = new List<NsqlConfigDocument>();
        foreach (var file in files)
        {
            var read = await NsqlReader.ReadConfigFile(file, cancellationToken);
            diagnostics.AddRange(read.Diagnostics);
            if (read.Value is { } document)
            {
                documents.Add(document);
            }
        }

        ThrowOnErrors(diagnostics);
        return documents;
    }

    // The overlay wins per statement kind, so an environment can replace the base DATABASE/STATE/ENGINE wholesale
    // (e.g. a STATE s3 statement cleanly supersedes a base STATE file). PLUGIN declarations are project-wide, so
    // both layers' pass through — a base declaration stays resolvable from an overlay's reference.
    private static List<NsqlConfigDocument> Merge(List<NsqlConfigDocument> baseLayer, List<NsqlConfigDocument> overlay)
    {
        var replaceDatabase = overlay.Any(d => d.Statements.OfType<DatabaseStatement>().Any());
        var replaceState = overlay.Any(d => d.Statements.OfType<StateStatement>().Any());
        var replaceEngine = overlay.Any(d => d.Statements.OfType<EngineStatement>().Any());

        var kept = baseLayer
            .Select(document => new NsqlConfigDocument([.. document.Statements.Where(Keep)]) { FilePath = document.FilePath })
            .Where(document => document.Statements.Count > 0);

        return [.. kept, .. overlay];

        bool Keep(ConfigStatement statement) => statement switch
        {
            DatabaseStatement => !replaceDatabase,
            StateStatement => !replaceState,
            EngineStatement => !replaceEngine,
            _ => true,
        };
    }

    // The CLI is the single presenter of errors, and a broken configuration is fail-fast: join every error into
    // one thrown message, each finding naming its file and position.
    private static void ThrowOnErrors(IEnumerable<NsqlDiagnostic> diagnostics)
    {
        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(Describe).ToList();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        }
    }

    private static string Describe(NsqlDiagnostic diagnostic) =>
        diagnostic.File is { } file && diagnostic.Position != SourcePosition.None
            ? $"{diagnostic.Message} ({file}:{diagnostic.Position.Line})"
            : diagnostic.Message;

    // ENGINE asserts the engine version range a project requires; the engine is compiled into the CLI, so the fix
    // for a mismatch is updating the tool, not restoring a package.
    private static void ValidateEngineRequirement(EngineConfig? engine)
    {
        if (engine is null)
        {
            return;
        }

        var host = typeof(NSchemaApplication).Assembly.GetName().Version!;
        var version = new SemanticVersion(host.Major, host.Minor, Math.Max(host.Build, 0), Math.Max(host.Revision, 0), Prerelease: null);
        var validated = engine.Requirement.Validate(version);
        if (validated.IsFailure)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine,
                validated.Errors.Select(e => $"{e.Message} Update the tool with: dotnet tool update NSchema --global")));
        }
    }
}
