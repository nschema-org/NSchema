using System.Text;

namespace NSchema.Commands.Scaffold;

/// <summary>
/// Composes a starter NSchema project from caller-supplied config blocks and a sample schema, writing the files to
/// disk. It is deliberately pure plumbing: the provider/backend blocks and the sample schema come from the plugins
/// (resolved by the command), so the only thing the CLI itself owns here is the file framing and the built-in
/// local-file backend, which has no plugin.
/// </summary>
internal static class ProjectScaffolder
{
    private const string ConfigFileName = "config.sql";
    private const string EnvironmentOverlayFileName = "config.env.prod.sql";
    private const string SchemaDirectoryName = "schemas";

    private const string ConfigHeader =
        """
        -- NSchema project configuration. These blocks tell NSchema which database to
        -- connect to and where to keep state. Config blocks may live in any .sql file.
        """;

    private const string OverlayHeader =
        """
        -- Overlay for the 'prod' environment. Select it with:
        --   nschema plan --environment prod
        -- Any base block you don't override here still applies.
        """;

    // The local-file state store is built into the core, so — unlike every other backend — it has no plugin to render
    // its block. The CLI owns these two.
    private const string FileBackendBlock =
        """
        BACKEND file (
          path = './nschema.state.json'
        );
        """;

    private const string FileBackendOverlayBlock =
        """
        BACKEND file (
          path = './nschema.prod.state.json'
        );
        """;

    /// <summary>
    /// Writes the starter files into <paramref name="directory"/>, returning the created paths (relative to it).
    /// </summary>
    /// <param name="directory">The directory to scaffold into.</param>
    /// <param name="force">Force the scaffolding even if the directory is not empty.</param>
    /// <param name="providerBlock">The provider's <c>PROVIDER</c> config block, rendered by the provider plugin.</param>
    /// <param name="sampleSchema">The provider's dialect-specific sample schema.</param>
    /// <param name="pluginBackend">
    /// The plugin-rendered backend blocks (base + environment overlay), or <see langword="null"/> to use the
    /// built-in local-file backend.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <exception cref="InvalidOperationException">The directory is not empty, and <paramref name="force"/> is false.</exception>
    public static async Task<IReadOnlyList<string>> Scaffold(
        string directory,
        bool force,
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
        await File.WriteAllTextAsync(configPath, Compose(ConfigHeader, providerBlock, backendBlock), cancellationToken);

        var overlayPath = Path.Combine(directory, EnvironmentOverlayFileName);
        await File.WriteAllTextAsync(overlayPath, Compose(OverlayHeader, overlayBackendBlock), cancellationToken);

        var sampleRelativePath = Path.Combine(SchemaDirectoryName, "example.sql");
        var samplePath = Path.Combine(directory, sampleRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(samplePath)!);
        await File.WriteAllTextAsync(samplePath, sampleSchema.TrimEnd() + "\n", cancellationToken);

        return [ConfigFileName, EnvironmentOverlayFileName, sampleRelativePath];
    }

    // Joins a header and one or more config blocks into a file body: blank-line separated, single trailing newline.
    private static string Compose(string header, params string[] blocks)
    {
        var builder = new StringBuilder();
        builder.Append(header.TrimEnd()).Append('\n');
        foreach (var block in blocks)
        {
            builder.Append('\n').Append(block.TrimEnd()).Append('\n');
        }

        return builder.ToString();
    }
}
