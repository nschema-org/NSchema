using NSchema.Configuration.Plugins;
using NSchema.Configuration.State;
using NSchema.Plugins.Model;
using NSchema.Plugins.Model.Config;

namespace NSchema.Tests;

/// <summary>
/// Builds configured provider/state values for tests that only need <em>presence</em> (a provider or backend "is
/// configured"), independent of which plugin it resolves to.
/// </summary>
internal static class TestConfigs
{
    public static PluginReference Provider(string label = "postgres", string packageId = "NSchema.Postgres") =>
        Reference(label, packageId);

    public static StateConfig S3State() =>
        new() { Plugin = Reference("s3", "NSchema.Aws") };

    private static PluginReference Reference(string label, string packageId) =>
        new(packageId, "5.0.0", "[5.0.0]", label, new PluginConfig(new PluginLabel(label), new Dictionary<AttributeKey, ConfigValue>()));
}
