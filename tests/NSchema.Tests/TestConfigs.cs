using NSchema.Configuration;
using NSchema.Configuration.Plugins;
using NSchema.Configuration.Provider;
using NSchema.Configuration.State;

namespace NSchema.Tests;

/// <summary>
/// Builds configured provider/state slices for tests that only need <em>presence</em> (a provider or backend "is
/// configured"), independent of which plugin it resolves to.
/// </summary>
internal static class TestConfigs
{
    public static ProviderConfig Provider(string label = "postgres", string packageId = "NSchema.Postgres") =>
        new() { Plugin = Reference("provider", label, packageId) };

    public static StateConfig S3State() =>
        new() { Plugin = Reference("backend", "s3", "NSchema.Aws") };

    private static PluginReference Reference(string blockType, string label, string packageId) =>
        new(packageId, "4.0.0", label, new ConfigBlock(blockType, label, new Dictionary<string, ConfigValue>()));
}
