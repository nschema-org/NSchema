using NSchema.Configuration;
using NSchema.Configuration.Plugins;
using NSchema.Configuration.State;

namespace NSchema.Tests;

/// <summary>
/// Builds configured provider/state values for tests that only need <em>presence</em> (a provider or backend "is
/// configured"), independent of which plugin it resolves to.
/// </summary>
internal static class TestConfigs
{
    public static PluginReference Provider(string label = "postgres", string packageId = "NSchema.Postgres") =>
        Reference("provider", label, packageId);

    public static StateConfig S3State() =>
        new() { Plugin = Reference("backend", "s3", "NSchema.Aws") };

    private static PluginReference Reference(string blockType, string label, string packageId) =>
        new(packageId, "4.0.0", label, new ConfigBlock(blockType, label, new Dictionary<string, ConfigValue>()));
}
