using NSchema.Configuration;
using NSchema.Configuration.Plugins;
using NSchema.Extensions;
using NSchema.Plugins;
using NSchema.Sql;

namespace NSchema.Tests.Configuration.Plugins;

/// <summary>
/// The keystone end-to-end proof of the plugin architecture: restores the real published <c>NSchema.Postgres</c>
/// plugin from nuget.org, loads it into an isolated <see cref="System.Runtime.Loader.AssemblyLoadContext"/>, and
/// drives it through the contract. Requires the .NET SDK and network access (it runs <c>dotnet publish</c>) — slow
/// on first run while the closure is restored, cached thereafter.
/// </summary>
public sealed class PluginLoaderTests : IDisposable
{
    private readonly string _cacheRoot = Path.Combine(Path.GetTempPath(), "nschema-plugin-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_cacheRoot))
        {
            Directory.Delete(_cacheRoot, recursive: true);
        }
    }

    [Fact]
    public void Load_RealPostgresPlugin_DiscoversProviderAndConfiguresHost()
    {
        // Arrange
        var loader = new PluginLoader(_cacheRoot);

        // Act — restore + load + discover
        var plugin = loader.Load("NSchema.Postgres", "4.0.0-alpha.2")
            .ValueOrThrow()
            .OfType<INSchemaProviderPlugin>()
            .Single();

        // Assert — discovery
        plugin.Label.ShouldBe("postgres");

        // Act — drive the plugin (in its own ALC) against the host's builder
        var builder = NSchemaApplication.CreateBuilder();
        var block = new ConfigBlock("provider", "postgres", new Dictionary<string, ConfigValue>
        {
            ["connection_string"] = ConfigValue.OfString("Host=localhost;Database=app"),
        });
        var result = plugin.Configure(builder, block);

        // Assert — the plugin registered the HOST's ISqlGenerator: proof the contract types unify across the
        // AssemblyLoadContext boundary (Core was deferred to the default context).
        result.Succeeded.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
        builder.Services.ShouldContain(d => d.ServiceType == typeof(ISqlGenerator));
    }

    [Fact]
    public void ResolveLatestVersion_ReturnsAVersionInTheHostMajor()
    {
        // Arrange
        var loader = new PluginLoader(_cacheRoot);

        // Act — floats NSchema.Postgres within this CLI's NSchema.Core major and reads back the resolved version.
        var version = loader.ResolveLatestVersion("NSchema.Postgres");

        // Assert — a concrete 4.x version is pinned (the exact build floats as new ones publish).
        version.ShouldStartWith("4.");
    }

    [Fact]
    public void Load_WithoutRestore_WhenNotCached_FailsWithDiagnostic()
    {
        // Arrange — a fresh cache, so the plugin is not present.
        var loader = new PluginLoader(_cacheRoot);

        // Act — allowRestore: false (the --no-init path) must not reach the network; it fails fast as a diagnostic,
        // not an exception. No restore happens here, so this case needs neither the SDK nor network.
        var result = loader.Load("NSchema.Postgres", "4.0.0-alpha.2", allowRestore: false);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.Select(e => e.Message).ShouldContain(m => m.Contains("--no-init"));
    }

    [Fact]
    public void Load_WithoutRestore_AfterCaching_Succeeds()
    {
        // Arrange — warm the cache with a normal (restoring) load.
        var loader = new PluginLoader(_cacheRoot);
        loader.Load("NSchema.Postgres", "4.0.0-alpha.2").ValueOrThrow();

        // Act — a subsequent cache-only load (the --no-init path) succeeds without restoring.
        var plugin = loader.Load("NSchema.Postgres", "4.0.0-alpha.2", allowRestore: false)
            .ValueOrThrow()
            .OfType<INSchemaProviderPlugin>()
            .Single();

        // Assert
        plugin.Label.ShouldBe("postgres");
    }
}
