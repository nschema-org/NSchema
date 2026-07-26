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

    // The configuring statements: one of each, and the only ones a layer can override.
    private static readonly SettingsKeyword[] _configuring =
        [SettingsKeyword.Database, SettingsKeyword.State, SettingsKeyword.Engine];

    /// <summary>
    /// Layers <paramref name="higher"/> over <paramref name="lower"/>: a configuring statement that restates one
    /// from below under the same label refines it setting by setting, so an overlay carries only what differs. A
    /// different label is a different plugin, so it replaces outright — there is nothing meaningful to merge.
    /// Every other statement from both layers carries through.
    /// </summary>
    private static List<NsqlDocument> Merge(IReadOnlyList<NsqlDocument> lower, IReadOnlyList<NsqlDocument> higher)
    {
        var baseline = lower
            .SelectMany(document => document.Statements.OfType<SettingsStatement>())
            .Where(statement => _configuring.Contains(statement.Keyword))
            .ToLookup(statement => statement.Keyword);

        var overridden = higher
            .SelectMany(document => document.Statements.OfType<SettingsStatement>())
            .Select(statement => statement.Keyword)
            .ToHashSet();

        // The lower layer keeps everything the higher one does not restate; what it does restate is folded into the
        // higher statement, so the assembler still sees exactly one of each.
        var kept = lower
            .Select(document => document with { Statements = [.. document.Statements.Where(Keep)] })
            .Where(document => document.Statements.Count > 0);

        var refined = higher.Select(document => document with { Statements = [.. document.Statements.Select(Refine)] });

        return [.. kept, .. refined];

        bool Keep(NsqlStatement statement) =>
            statement is not SettingsStatement settings
            || !_configuring.Contains(settings.Keyword)
            || !overridden.Contains(settings.Keyword);

        NsqlStatement Refine(NsqlStatement statement) =>
            statement is SettingsStatement settings
            && _configuring.Contains(settings.Keyword)
            && baseline[settings.Keyword].FirstOrDefault() is { } original
            && original.Label?.Value == settings.Label?.Value
                ? original.WithSettingsFrom(settings)
                : statement;
    }

    private static SemanticVersion ReadEngineVersion()
    {
        var version = typeof(ConfigurationProvider).Assembly.GetName().Version!;
        return new SemanticVersion(version.Major, version.Minor, Math.Max(version.Build, 0), Math.Max(version.Revision, 0), Prerelease: null);
    }
}
