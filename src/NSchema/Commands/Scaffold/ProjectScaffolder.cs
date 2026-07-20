using System.Text;

namespace NSchema.Commands.Scaffold;

/// <summary>
/// Composes a starter NSchema project from caller-supplied config statements and a sample schema, writing the files
/// to disk. It is deliberately pure plumbing: the DATABASE/STATE statements and the sample schema come from the
/// plugins (resolved by the command), so the CLI itself owns only the file framing, the <c>PLUGIN</c> declarations
/// (it knows the package and resolved version), and the built-in local-file state store, which has no plugin.
/// </summary>
internal static class ProjectScaffolder
{
    private const string ConfigFileName = "config.env.sql";
    private const string EnvironmentOverlayFileName = "config.env.prod.sql";
    private const string SchemaDirectoryName = "schemas";

    private const string ConfigHeader =
        """
        -- NSchema project configuration. These statements declare the plugins the
        -- project depends on, which database to connect to, and where to keep state.
        -- The .env. marker makes a file configuration: *.env.sql loads for every
        -- environment, *.env.<name>.sql only for that one. Every other .sql file is schema DDL.
        """;

    private const string OverlayHeader =
        """
        -- Overlay for the 'prod' environment. Select it with:
        --   nschema plan --environment prod
        -- Any base statement you don't override here still applies.
        """;

    // The local-file state store is built into the core, so — unlike every other backend — it has no plugin to render
    // its statement. The CLI owns these two.
    private const string FileBackendBlock =
        """
        STATE file (
          path = './nschema.state.json'
        );
        """;

    private const string FileBackendOverlayBlock =
        """
        STATE file (
          path = './nschema.prod.state.json'
        );
        """;

    /// <summary>
    /// Writes the starter files into <paramref name="directory"/>, returning the created paths (relative to it).
    /// </summary>
    /// <param name="directory">The directory to scaffold into.</param>
    /// <param name="force">Force the scaffolding even if the directory is not empty.</param>
    /// <param name="plugins">The <c>PLUGIN</c> declarations to author: each plugin's label, package id, and pinned version.</param>
    /// <param name="providerBlock">The provider's <c>DATABASE</c> statement, rendered by the database plugin.</param>
    /// <param name="sampleSchema">The provider's dialect-specific sample schema.</param>
    /// <param name="pluginBackend">
    /// The plugin-rendered state statements (base + environment overlay), or <see langword="null"/> to use the
    /// built-in local-file state store.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <exception cref="InvalidOperationException">The directory is not empty, and <paramref name="force"/> is false.</exception>
    public static async Task<IReadOnlyList<string>> Scaffold(
        string directory,
        bool force,
        IReadOnlyList<(string Label, string PackageId, string Version)> plugins,
        string providerBlock,
        string sampleSchema,
        (string Base, string Overlay)? pluginBackend,
        CancellationToken cancellationToken = default)
    {
        if (Directory.GetFiles(directory, "*.*", SearchOption.AllDirectories).Length != 0 && !force)
        {
            throw new InvalidOperationException($"{directory} is not empty. Use --force to override.");
        }

        var backendBlock = pluginBackend?.Base ?? FileBackendBlock;
        var overlayBackendBlock = pluginBackend?.Overlay ?? FileBackendOverlayBlock;

        var configPath = Path.Combine(directory, ConfigFileName);
        await File.WriteAllTextAsync(configPath, Compose(ConfigHeader, [PluginDeclarations(plugins), providerBlock, backendBlock]), cancellationToken);

        var overlayPath = Path.Combine(directory, EnvironmentOverlayFileName);
        await File.WriteAllTextAsync(overlayPath, Compose(OverlayHeader, [overlayBackendBlock]), cancellationToken);

        var sampleRelativePath = Path.Combine(SchemaDirectoryName, "example.sql");
        var samplePath = Path.Combine(directory, sampleRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(samplePath)!);
        await File.WriteAllTextAsync(samplePath, sampleSchema.TrimEnd() + "\n", cancellationToken);

        return [ConfigFileName, EnvironmentOverlayFileName, sampleRelativePath];
    }

    // The host authors the PLUGIN statements: it resolved the packages, so it knows the pins.
    private static string PluginDeclarations(IReadOnlyList<(string Label, string PackageId, string Version)> plugins) =>
        string.Join("\n\n", plugins.Select(p =>
            $"PLUGIN {p.Label} (\n  source  = '{p.PackageId}',\n  version = '{p.Version}'\n);"));

    // Joins a header and one or more config statements into a file body: blank-line separated, single trailing newline.
    private static string Compose(string header, IReadOnlyList<string> blocks)
    {
        var builder = new StringBuilder();
        builder.Append(header.TrimEnd()).Append('\n');
        foreach (var block in blocks.Where(b => b.Length > 0))
        {
            builder.Append('\n').Append(block.TrimEnd()).Append('\n');
        }

        return builder.ToString();
    }
}
