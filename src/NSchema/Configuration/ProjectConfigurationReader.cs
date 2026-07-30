using NSchema.Configuration.Domain;
using NSchema.Configuration.Plugins;
using NSchema.Configuration.State;
using NSchema.Project.Nsql;

namespace NSchema.Configuration;

/// <summary>
/// Reads project configuration files under a directory.
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
    public static async ValueTask<Result<ProjectConfiguration>> Read(string root, string? environment, CancellationToken cancellationToken = default)
    {
        var definition = await ReadDefinition(root, environment, cancellationToken);
        if (definition.IsFailure)
        {
            return Result.Failure<ProjectConfiguration>(definition.Diagnostics);
        }

        var lockFile = (await LockFileManager.Read(LockFilePath(root), cancellationToken)).Require();

        return Assemble(definition.Require(), (source, range) =>
            lockFile.Find(source)?.Version
            ?? Result.Failure<SemanticVersion>(PluginDiagnostics.NotLocked(source, range)));
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
    internal static async ValueTask<Result<ProjectConfiguration>> Refresh(string root, string? environment, LockFile existing, PluginLoader loader, Func<PackageId, bool>? refresh, CancellationToken cancellationToken = default)
    {
        var definition = await ReadDefinition(root, environment, cancellationToken);
        if (definition.IsFailure)
        {
            return Result.Failure<ProjectConfiguration>(definition.Diagnostics);
        }

        return Assemble(definition.Require(), (source, range) =>
        {
            // Editing a PLUGIN version should outrank a stale pin.
            var reuse = refresh is null || !refresh(source);
            if (reuse && existing.Find(source)?.Version is { } locked && range.Satisfies(locked))
            {
                return locked;
            }

            // An exact pin is its own resolution; only a range reaches the feed.
            return range.ExactVersion ?? loader.ResolveHighest(source, range);
        });
    }

    // Each slice resolves independently so a project with two broken statements reports both, rather than only
    // whichever one happened to be assembled first.
    private static Result<ProjectConfiguration> Assemble(ConfigurationDefinition definition, Func<PackageId, VersionRange, Result<SemanticVersion>> resolve)
    {
        var database = definition.Database is { } db ? PluginReference.Resolve(db, definition.Plugins, resolve) : null;
        var state = definition.State is { } st ? StateConfiguration.Resolve(st, definition.Plugins, resolve) : null;

        var diagnostics = Enumerable.Empty<Diagnostic>()
            .Concat(database?.Diagnostics ?? Enumerable.Empty<Diagnostic>())
            .Concat(state?.Diagnostics ?? Enumerable.Empty<Diagnostic>())
            .ToList();

        return Result.From(
            new ProjectConfiguration
            {
                Plugins = definition.Plugins,
                Database = database?.Value,
                State = state?.Value,
            },
            diagnostics);
    }

    /// <summary>
    /// Reads the project's <c>PLUGIN</c> declarations, without resolving ranges — used to map a plugin label to its
    /// package before resolution.
    /// </summary>
    internal static async ValueTask<Result<IReadOnlyList<PluginDeclaration>>> ReadDeclarations(string root, string? environment, CancellationToken cancellationToken = default) =>
        (await ReadDefinition(root, environment, cancellationToken)).Map(definition => definition.Plugins);

    // Core owns reading, layering, assembly, and ENGINE enforcement; the CLI resolves the files each layer covers
    // (it owns globbing) and supplies its own version so an ENGINE host_version assertion is checked against the tool.
    private static async ValueTask<Result<ConfigurationDefinition>> ReadDefinition(string root, string? environment, CancellationToken cancellationToken)
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
                return ConfigurationDiagnostics.UnknownEnvironment(environment);
            }

            layers.Add(new ConfigurationLayer(overlayFiles));
        }

        var loaded = await ConfigurationProvider.Load(layers, HostVersion.Current, cancellationToken);

        // The parser's findings carry a file and position of their own, which the diagnostics table does not render;
        // fold the location into the message so each one keeps pointing at the line the reader has to go and edit.
        return Result.From(loaded.Value, loaded.Diagnostics.Select(Describe));
    }

    // The finding keeps its own source and code — they say what produced it and which finding it is, neither of which
    // is a location — so only the text is rewritten.
    private static Diagnostic Describe(NsqlDiagnostic diagnostic) =>
        Location(diagnostic) is { } location ? diagnostic with { Text = $"{location}: {diagnostic.Text}" } : diagnostic;

    private static string? Location(NsqlDiagnostic diagnostic) => diagnostic.File is not { } file
        ? null
        : diagnostic.Position != SourcePosition.None
            ? $"{Path.GetFileName(file)}:{diagnostic.Position.Line}"
            : Path.GetFileName(file);
}
