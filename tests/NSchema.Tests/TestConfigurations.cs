using NSchema.Configuration.Model;
using NSchema.Configuration.Plugins;
using NSchema.Configuration.State;

namespace NSchema.Tests;

/// <summary>
/// Builds configured provider/state values for tests that only need <em>presence</em> (a provider or backend "is
/// configured"), independent of which plugin it resolves to.
/// </summary>
internal static class TestConfigurations
{
    public static PluginReference Provider(string label = "postgres", string packageId = "NSchema.Postgres") =>
        Reference(label, packageId);

    public static StateConfiguration S3State() =>
        new() { Plugin = Reference("s3", "NSchema.Aws") };

    private static PluginReference Reference(string label, string packageId) =>
        new(new PackageId(packageId), SemanticVersion.Parse("5.0.0"), new PluginLabel(label), new PluginSettings(new PluginLabel(label), new Dictionary<string, string?>()));
}
