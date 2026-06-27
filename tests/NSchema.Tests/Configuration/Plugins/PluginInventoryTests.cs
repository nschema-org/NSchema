using NSchema.Configuration;
using NSchema.Configuration.Plugins;
using NSchema.Configuration.State;

namespace NSchema.Tests.Configuration.Plugins;

public sealed class PluginInventoryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "nschema-inventory-tests", Guid.NewGuid().ToString("N"));
    private readonly PluginCache _cache;

    public PluginInventoryTests() => _cache = new PluginCache(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static PluginReference Reference(string packageId, string version, string label) =>
        new(packageId, version, label, new ConfigBlock(label == "s3" ? "backend" : "provider", label, new Dictionary<string, ConfigValue>()));

    private void Seed(string packageId, string version)
    {
        var publish = _cache.PublishDirectory(packageId, version);
        Directory.CreateDirectory(publish);
        File.WriteAllText(System.IO.Path.Combine(publish, packageId + ".dll"), "x");
    }

    [Fact]
    public void ForProject_NothingConfigured_ReturnsEmpty()
    {
        // Act + Assert
        PluginInventory.ForProject(provider: null, state: null, _cache).ShouldBeEmpty();
    }

    [Fact]
    public void ForProject_FileBackend_IsNotListedAsAPlugin()
    {
        // Arrange — the built-in file store is not a plugin.
        var state = new StateConfig { File = new FileStateConfig { Path = "state.json" } };

        // Act + Assert
        PluginInventory.ForProject(provider: null, state, _cache).ShouldBeEmpty();
    }

    [Fact]
    public void ForProject_ProviderAndBackend_DescribesRolesAndCacheStatus()
    {
        // Arrange — provider restored, backend not.
        Seed("NSchema.Postgres", "4.0.0");
        var provider = Reference("NSchema.Postgres", "4.0.0", "postgres");
        var state = new StateConfig { Plugin = Reference("NSchema.Aws", "4.0.0", "s3") };

        // Act
        var inventory = PluginInventory.ForProject(provider, state, _cache);

        // Assert
        var providerEntry = inventory.Single(p => p.Role == PluginInventory.ProviderRole);
        providerEntry.Label.ShouldBe("postgres");
        providerEntry.Restored.ShouldBeTrue();
        providerEntry.CachePath.ShouldNotBeNull();

        var backendEntry = inventory.Single(p => p.Role == PluginInventory.BackendRole);
        backendEntry.Label.ShouldBe("s3");
        backendEntry.Restored.ShouldBeFalse();
        backendEntry.CachePath.ShouldBeNull();
    }
}
