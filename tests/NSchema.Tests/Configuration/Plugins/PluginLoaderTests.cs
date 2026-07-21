using NSchema.Configuration.Model;
using NSchema.Configuration.Plugins;
using NSchema.Plan.Backends;
using NSchema.Plugins;

namespace NSchema.Tests.Configuration.Plugins;

/// <summary>
/// The keystone end-to-end proof of the plugin architecture: restores the real published <c>NSchema.Postgres</c>
/// plugin from nuget.org, loads it into an isolated <see cref="System.Runtime.Loader.AssemblyLoadContext"/>, and
/// drives it through the contract. Requires the .NET SDK and network access (it runs <c>dotnet publish</c>) — slow
/// on first run while the closure is restored, cached thereafter.
/// </summary>
public sealed class PluginLoaderTests : IDisposable
{
    private static readonly PackageId Package = new("NSchema.Postgres");
    private static readonly SemanticVersion Version = SemanticVersion.Parse("5.0.0-alpha.5");

    private readonly string _cacheRoot = Path.Combine(Path.GetTempPath(), "nschema-plugin-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_cacheRoot))
        {
            Directory.Delete(_cacheRoot, recursive: true);
        }
    }

    [Fact]
    public void Load_RealPostgresPlugin_DiscoversDatabasePluginAndConfiguresHost()
    {
        // Arrange
        var loader = new PluginLoader(_cacheRoot);

        // Act — restore + load + discover by capability (a plugin has no name of its own).
        var plugin = loader.Load(Package, Version)
            .Require()
            .OfType<INSchemaDatabasePlugin>()
            .Single();

        // Act — drive the plugin (in its own ALC) against the host's builder
        var builder = NSchemaApplication.CreateBuilder();
        var config = new PluginSettings(new PluginLabel("postgres"), new Dictionary<string, string?>
        {
            ["connection_string"] = "Host=localhost;Database=app",
        });
        var result = plugin.Configure(builder, config);

        // Assert — the plugin registered the HOST's SqlDialect: proof the contract types unify across the
        // AssemblyLoadContext boundary (Core was deferred to the default context).
        result.IsSuccess.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
        builder.Services.ShouldContain(d => d.ServiceType == typeof(SqlDialect));
    }

    [Fact]
    public void ResolveLatestVersion_ReturnsAVersionInTheHostMajor()
    {
        // Arrange
        var loader = new PluginLoader(_cacheRoot);

        // Act — floats NSchema.Postgres within this CLI's NSchema.Core major and reads back the resolved version.
        var version = loader.ResolveLatestVersion(Package);

        // Assert — a concrete 5.x version is pinned (the exact build floats as new ones publish).
        version.ToString().ShouldStartWith("5.");
    }

    [Fact]
    public void Load_RacingColdCache_AllConcurrentCallersSucceed()
    {
        // Arrange — several loaders share one cold cache, mirroring parallel integration tests that each spin up their
        // own throwaway database and restore the same plugin at the same moment.
        const int concurrency = 4;
        var loaded = new bool[concurrency];

        // Act — race the restore. Before the cross-process restore lock this collided inside `dotnet publish`
        // ("the process cannot access the file ... because it is being used by another process").
        Parallel.For(0, concurrency, i => loaded[i] = new PluginLoader(_cacheRoot)
            .Load(Package, Version)
            .Require()
            .OfType<INSchemaDatabasePlugin>()
            .Any());

        // Assert — every racer ended up with the working plugin, and exactly one publish closure landed in the cache.
        loaded.ShouldAllBe(found => found);
        new PluginCache(_cacheRoot).List().ShouldHaveSingleItem();
    }

    [Fact]
    public void Load_WithoutRestore_WhenNotCached_FailsWithDiagnostic()
    {
        // Arrange — a fresh cache, so the plugin is not present.
        var loader = new PluginLoader(_cacheRoot);

        // Act — allowRestore: false (the --no-init path) must not reach the network; it fails fast as a diagnostic,
        // not an exception. No restore happens here, so this case needs neither the SDK nor network.
        var result = loader.Load(Package, Version, allowRestore: false);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.Select(e => e.Message).ShouldContain(m => m.Contains("--no-init"));
    }

    [Fact]
    public void Load_WithoutRestore_AfterCaching_Succeeds()
    {
        // Arrange — warm the cache with a normal (restoring) load.
        var loader = new PluginLoader(_cacheRoot);
        loader.Load(Package, Version).Require();

        // Act — a subsequent cache-only load (the --no-init path) succeeds without restoring.
        var plugins = loader.Load(Package, Version, allowRestore: false).Require();

        // Assert
        plugins.OfType<INSchemaDatabasePlugin>().ShouldHaveSingleItem();
    }
}
