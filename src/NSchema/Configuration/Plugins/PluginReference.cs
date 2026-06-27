namespace NSchema.Configuration.Plugins;

/// <summary>
/// A resolved reference to the NuGet package that supplies a plugin.
/// </summary>
internal sealed record PluginReference(string PackageId, string Version, string Label, ConfigBlock Block)
{
    private const string VersionAttribute = "version";
    private const string SourceAttribute = "source";

    /// <summary>The first-party provider plugins, keyed by their <c>PROVIDER</c> block label.</summary>
    private static readonly IReadOnlyDictionary<string, string> ProviderPackages =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["postgres"] = "NSchema.Postgres",
            ["sqlite"] = "NSchema.Sqlite",
            ["sqlserver"] = "NSchema.SqlServer",
        };

    /// <summary>The first-party backend plugins, keyed by their <c>BACKEND</c> block label.</summary>
    private static readonly IReadOnlyDictionary<string, string> BackendPackages =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["s3"] = "NSchema.Aws",
        };

    /// <summary>Resolves the plugin reference for a <c>PROVIDER</c> block.</summary>
    public static PluginReference ForProvider(ConfigBlock block) => FromBlock(block, ProviderPackages);

    /// <summary>Resolves the plugin reference for a <c>BACKEND</c> block (the non-file backends).</summary>
    public static PluginReference ForBackend(ConfigBlock block) => FromBlock(block, BackendPackages);

    /// <summary>
    /// Resolves the plugin reference for a configuration block.
    /// </summary>
    /// <param name="block">The <c>PROVIDER</c> or <c>BACKEND</c> block.</param>
    /// <param name="builtInPackages">The label-to-package-id map for the first-party plugins of this block kind.</param>
    public static PluginReference FromBlock(ConfigBlock block, IReadOnlyDictionary<string, string> builtInPackages)
    {
        var kind = block.Type.ToUpperInvariant();

        var label = block.Label?.ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(label))
        {
            throw new InvalidOperationException($"A {kind} block is missing its label, e.g. {kind} postgres ( ... ).");
        }

        var source = block.Attribute(SourceAttribute)?.AsString();
        var packageId = !string.IsNullOrWhiteSpace(source)
            ? source
            : builtInPackages.TryGetValue(label, out var builtIn)
                ? builtIn
                : throw new InvalidOperationException(
                    $"Unknown {kind} '{label}'. Name its plugin package with a 'source' attribute, e.g. source = \"Acme.NSchema.{label}\".");

        if (!IsValidPackageId(packageId))
        {
            throw new InvalidOperationException($"'{packageId}' is not a valid NuGet package id for {kind} '{label}'.");
        }

        var version = block.Attribute(VersionAttribute)?.AsString();
        if (string.IsNullOrWhiteSpace(version))
        {
            throw new InvalidOperationException($"{kind} '{label}' must pin a plugin version, e.g. version = \"4.0.0\".");
        }

        return new PluginReference(packageId, version, label, StripPluginAttributes(block));
    }

    // 'version' and 'source' are CLI-level wiring, not part of the plugin's own configuration vocabulary, so they
    // are removed before the block reaches the plugin.
    private static ConfigBlock StripPluginAttributes(ConfigBlock block)
    {
        var attributes = new Dictionary<string, ConfigValue>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in block.Attributes)
        {
            if (!key.Equals(VersionAttribute, StringComparison.OrdinalIgnoreCase)
                && !key.Equals(SourceAttribute, StringComparison.OrdinalIgnoreCase))
            {
                attributes[key] = value;
            }
        }

        return block with { Attributes = attributes };
    }

    private static bool IsValidPackageId(string id) =>
        id.Length > 0 && id.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-');
}
