using NSchema.Configuration.Model;
using NSchema.Configuration.Plugins;
using NSchema.Configuration.State;
using NSchema.Project.Nsql;

namespace NSchema.Configuration;

/// <summary>
/// Reads project configuration from the <c>*.env.sql</c> (and selected <c>*.env.&lt;env&gt;.sql</c>) files under a directory.
/// </summary>
internal static class ProjectConfigurationReader
{
    /// <summary>
    /// The lockfile that pins declared version ranges to concrete versions, alongside the project's config files.
    /// </summary>
    public static string LockFilePath(string root) => Path.Combine(root, "nschema.lock");

    /// <summary>
    /// Reads the project configuration.
    /// </summary>
    /// <param name="root">The project directory.</param>
    /// <param name="environment">The target environment, or <see langword="null"/> for the base configuration only.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    public static async ValueTask<ProjectConfiguration> Read(string root, string? environment, CancellationToken cancellationToken = default)
    {
        var definition = await ReadDefinition(root, environment, cancellationToken);
        var lockFile = (await LockFileManager.Read(LockFilePath(root), cancellationToken)).Require();

        return Assemble(definition, (source, range) =>
            lockFile.Find(source)?.Version
            ?? throw new InvalidOperationException(
                $"Plugin '{source}' is declared as '{range}' but is not locked. Run 'nschema init' to resolve and lock it."));
    }

    /// <summary>
    /// Reads the project configuration, resolving declared ranges against the feed: an <paramref name="existing"/>
    /// pin is kept unless <paramref name="refresh"/> selects its package, in which case the range resolves to its
    /// highest available version. Used by <c>init</c>/<c>plugin update</c> to resolve-and-lock.
    /// </summary>
    /// <param name="root">The project directory.</param>
    /// <param name="environment">The target environment, or <see langword="null"/> for the base configuration only.</param>
    /// <param name="existing">The current lockfile, whose pins are kept unless refreshed.</param>
    /// <param name="loader">Resolves a range to its highest available version.</param>
    /// <param name="refresh">Selects which packages to re-resolve; <see langword="null"/> keeps every existing pin.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    internal static async ValueTask<ProjectConfiguration> Refresh(string root, string? environment, LockFile existing, PluginLoader loader, Func<PackageId, bool>? refresh, CancellationToken cancellationToken = default)
    {
        var definition = await ReadDefinition(root, environment, cancellationToken);

        return Assemble(definition, (source, range) =>
        {
            var reuse = refresh is null || !refresh(source);
            // An exact pin is its own resolution; only a range reaches the feed.
            return (reuse ? existing.Find(source)?.Version : null) ?? range.ExactVersion ?? loader.ResolveHighest(source, range);
        });
    }

    private static ProjectConfiguration Assemble(ConfigurationDefinition definition, Func<PackageId, VersionRange, SemanticVersion> resolve) =>
        new()
        {
            Plugins = definition.Plugins,
            Database = definition.Database is { } database ? PluginReference.Resolve(database, definition.Plugins, resolve) : null,
            State = definition.State is { } state ? StateConfiguration.Resolve(state, definition.Plugins, resolve) : null,
        };

    /// <summary>
    /// Reads the project's <c>PLUGIN</c> declarations, without resolving ranges — used to map a plugin label to its
    /// package before resolution.
    /// </summary>
    internal static async ValueTask<IReadOnlyList<PluginDeclaration>> ReadDeclarations(string root, string? environment, CancellationToken cancellationToken = default) =>
        (await ReadDefinition(root, environment, cancellationToken)).Plugins;

    // Core owns reading, layering, assembly, and ENGINE enforcement; the CLI resolves the files each layer covers
    // (it owns globbing) and supplies its own version so an ENGINE host_version assertion is checked against the tool.
    private static async ValueTask<ConfigurationDefinition> ReadDefinition(string root, string? environment, CancellationToken cancellationToken)
    {
        var layers = new List<ConfigurationLayer>
        {
            new(ProjectGlobs.Match(root, ProjectGlobs.Base())),
        };

        if (environment is not null)
        {
            var overlayFiles = ProjectGlobs.Match(root, ProjectGlobs.EnvironmentConfiguration(environment));
            if (overlayFiles.Count == 0)
            {
                throw new InvalidOperationException($"No configuration files found for environment '{environment}'.");
            }

            layers.Add(new ConfigurationLayer(overlayFiles));
        }

        var loaded = await ConfigurationProvider.Load(layers, HostVersion.Current, cancellationToken);
        ThrowOnErrors(loaded.Diagnostics);
        return loaded.Require();
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
}
