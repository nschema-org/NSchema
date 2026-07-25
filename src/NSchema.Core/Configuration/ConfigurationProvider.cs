using NSchema.Configuration.Domain;
using NSchema.Configuration.Engine;
using NSchema.Project.Nsql;
using NSchema.Project.Nsql.Syntax;
using NSchema.Project.Nsql.Syntax.Settings;

namespace NSchema.Configuration;

/// <summary>
/// The entry point from configuration sources to a validated <see cref="ConfigurationDefinition"/>: reads each
/// layer, merges them by precedence, assembles, and enforces the project's <c>ENGINE</c> assertion.
/// </summary>
public static class ConfigurationProvider
{
    // The engine's own version — what a project's ENGINE 'version' assertion is checked against.
    private static readonly SemanticVersion _engineVersion = ReadEngineVersion();

    /// <summary>
    /// Reads and resolves <paramref name="layers"/> into the configuration they declare. Later layers override
    /// earlier ones per statement kind — a <c>DATABASE</c>/<c>STATE</c>/<c>ENGINE</c> in a higher layer replaces
    /// the lower one.
    /// </summary>
    /// <param name="layers">The configuration layers, in increasing precedence.</param>
    /// <param name="hostVersion">The host tool's version, checked against an <c>ENGINE host_version</c> assertion; <see langword="null"/> when there is no host tool (the engine is embedded directly).</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    public static async Task<Result<ConfigurationDefinition, NsqlDiagnostic>> Load(IReadOnlyList<ConfigurationLayer> layers, SemanticVersion? hostVersion = null, CancellationToken cancellationToken = default)
    {
        var diagnostics = new List<NsqlDiagnostic>();

        List<NsqlDocument> merged = [];
        foreach (var layer in layers)
        {
            var documents = new List<NsqlDocument>();
            foreach (var path in layer.Paths)
            {
                var read = await NsqlReader.ReadFile(path, cancellationToken);
                diagnostics.AddRange(read.Diagnostics);
                if (read.Value is { } document)
                {
                    documents.Add(document);
                }
            }

            merged = merged.Count == 0 ? documents : Merge(merged, documents);
        }

        var assembled = ConfigurationAssembler.Assemble(merged);
        diagnostics.AddRange(assembled.Diagnostics);

        var definition = assembled.Value;
        if (definition?.Engine is { } engine)
        {
            Enforce(engine, hostVersion, diagnostics);
        }

        return Result<ConfigurationDefinition, NsqlDiagnostic>.From(definition, diagnostics);
    }

    /// <summary>
    /// Reads and resolves a single set of configuration files (one layer).
    /// </summary>
    public static Task<Result<ConfigurationDefinition, NsqlDiagnostic>> Load(IReadOnlyList<string> paths, SemanticVersion? hostVersion = null, CancellationToken cancellationToken = default) =>
        Load([new ConfigurationLayer(paths)], hostVersion, cancellationToken);

    // The ENGINE assertion is enforced here, not in the assembler: assembly only combines the sources, while
    // enforcement depends on the running engine (and the host that invokes it).
    private static void Enforce(EngineConfiguration engine, SemanticVersion? hostVersion, List<NsqlDiagnostic> diagnostics)
    {
        if (engine.Version is { } engineRange && !engineRange.Satisfies(_engineVersion))
        {
            diagnostics.Add(EngineDiagnostics.EngineRequirementUnsatisfied(engineRange, _engineVersion));
        }

        // Only a versioned host (e.g. the CLI) can satisfy a host_version assertion; there is nothing to check when
        // the engine is embedded directly.
        if (engine.HostVersion is { } hostRange && hostVersion != null && !hostRange.Satisfies(hostVersion))
        {
            diagnostics.Add(EngineDiagnostics.HostRequirementUnsatisfied(hostRange, hostVersion));
        }
    }

    // A higher layer replaces a lower layer's DATABASE/STATE/ENGINE wholesale (so an overlay can swap the state
    // store cleanly); every other statement from both layers carries through.
    private static List<NsqlDocument> Merge(IReadOnlyList<NsqlDocument> lower, IReadOnlyList<NsqlDocument> higher)
    {
        var replaceDatabase = higher.Any(d => d.Statements.OfType<SettingsStatement>().Any(s => s.Keyword == SettingsKeyword.Database));
        var replaceState = higher.Any(d => d.Statements.OfType<SettingsStatement>().Any(s => s.Keyword == SettingsKeyword.State));
        var replaceEngine = higher.Any(d => d.Statements.OfType<SettingsStatement>().Any(s => s.Keyword == SettingsKeyword.Engine));

        var kept = lower
            .Select(document => document with { Statements = [.. document.Statements.Where(Keep)] })
            .Where(document => document.Statements.Count > 0);

        return [.. kept, .. higher];

        bool Keep(NsqlStatement statement) => statement is not SettingsStatement settings || settings.Keyword switch
        {
            SettingsKeyword.Database => !replaceDatabase,
            SettingsKeyword.State => !replaceState,
            SettingsKeyword.Engine => !replaceEngine,
            _ => true,
        };
    }

    private static SemanticVersion ReadEngineVersion()
    {
        var version = typeof(ConfigurationProvider).Assembly.GetName().Version!;
        return new SemanticVersion(version.Major, version.Minor, Math.Max(version.Build, 0), Math.Max(version.Revision, 0), Prerelease: null);
    }
}
