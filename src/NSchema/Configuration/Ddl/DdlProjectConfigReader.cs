using NSchema.Configuration.Plugins;
using NSchema.Configuration.State;
using NSchema.Schema.Ddl;

namespace NSchema.Configuration.Ddl;

/// <summary>
/// Reads project configuration from the <c>.sql</c> files under a directory.
/// </summary>
internal static class DdlProjectConfigReader
{
    /// <summary>
    /// Reads the project configuration, layering the selected <paramref name="environment"/>'s overlay (if any).
    /// </summary>
    /// <param name="root">The project directory.</param>
    /// <param name="environment">The target environment, or <see langword="null"/> for the base configuration only.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    public static async ValueTask<DdlProjectConfig> Read(string root, string? environment, CancellationToken cancellationToken = default)
    {
        var baseConfig = await ReadLayer(ProjectGlobs.Match(root, ProjectGlobs.BaseSchema()), cancellationToken);

        if (environment is null)
        {
            return baseConfig;
        }

        var overlayFiles = ProjectGlobs.Match(root, ProjectGlobs.EnvironmentSchema(environment));
        if (overlayFiles.Count == 0)
        {
            throw new InvalidOperationException($"No files found for environment '{environment}'.");
        }

        var overlayConfig = await ReadLayer(overlayFiles, cancellationToken);
        return Merge(baseConfig, overlayConfig);
    }

    private static async ValueTask<DdlProjectConfig> ReadLayer(IReadOnlyList<string> files, CancellationToken cancellationToken)
    {
        var contents = await Task.WhenAll(files.Select(f => File.ReadAllTextAsync(f, cancellationToken)));
        var blocks = contents.Select(DdlReader.Instance.Read).SelectMany(d => d.Config).ToList();
        return Parse(blocks);
    }

    // The overlay wins per slice, so an environment can replace the base provider/backend/policy wholesale (e.g. a
    // BACKEND s3 block cleanly supersedes a base BACKEND file). Within each layer, the one-block-per-type rule holds.
    private static DdlProjectConfig Merge(DdlProjectConfig @base, DdlProjectConfig overlay) => new()
    {
        Provider = overlay.Provider ?? @base.Provider,
        State = overlay.State ?? @base.State,
    };

    private static DdlProjectConfig Parse(IReadOnlyList<ConfigBlock> blocks)
    {
        PluginReference? provider = null;
        StateConfig? state = null;

        foreach (var block in blocks)
        {
            switch (block.Type)
            {
                case "provider":
                    if (provider is not null)
                    {
                        throw Conflict("PROVIDER");
                    }
                    provider = PluginReference.ForProvider(block);
                    break;
                case "backend":
                    if (state is not null)
                    {
                        throw Conflict("BACKEND");
                    }
                    state = StateConfig.FromBlock(block);
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Unknown configuration block '{block.Type.ToUpperInvariant()}'. Expected PROVIDER or BACKEND.");
            }
        }

        return new DdlProjectConfig { Provider = provider, State = state };
    }

    private static InvalidOperationException Conflict(string blockType) =>
        new($"More than one {blockType} block is declared; specify exactly one across the project's .sql files.");
}
