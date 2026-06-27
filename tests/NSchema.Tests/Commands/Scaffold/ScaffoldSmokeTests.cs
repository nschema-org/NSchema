using NSchema.Commands.Scaffold;
using NSchema.Configuration.Ddl;
using NSchema.Configuration.Plugins;
using NSchema.Plugins;
using NSchema.Schema.Ddl;

namespace NSchema.Tests.Commands.Scaffold;

/// <summary>
/// End-to-end smoke test of the plugin-driven scaffold: loads the REAL published <c>NSchema.Postgres</c> plugin over
/// the ALC boundary, renders its config block (version pinned) and sample schema, composes a project with
/// <see cref="ProjectScaffolder"/>, and asserts the result parses, round-trips through the config reader, and is
/// already formatter-canonical (so <c>scaffold</c> followed by <c>fmt --check</c> is a no-op). Pins an exact version
/// so it resolves from the cache without a feed; the floating resolution is covered by <c>PluginLoaderTests</c>.
/// Requires the .NET SDK and network access (it may restore the plugin).
/// </summary>
public sealed class ScaffoldSmokeTests : IDisposable
{
    private const string Version = "4.0.0-alpha.2";

    private readonly string _directory = Directory.CreateTempSubdirectory("nschema-scaffold-smoke-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public async Task Scaffold_WithRealPostgresPlugin_ProducesAValidFormattedProject()
    {
        // Arrange — load the real plugin and render exactly what the scaffold command would.
        var plugin = new PluginLoader().Load("NSchema.Postgres", Version)
            .OfType<INSchemaProviderPlugin>()
            .Single();
        var providerBlock = plugin.GetScaffoldTemplate(new ScaffoldContext { Version = Version });
        var sampleSchema = plugin.GetSampleSchema();

        // Act — compose the project (file backend, like the default `nschema scaffold`).
        await ProjectScaffolder.Scaffold(_directory, force: false, providerBlock, sampleSchema, pluginBackend: null,
            TestContext.Current.CancellationToken);

        // Assert — the generated config round-trips, pinning the resolved version.
        var config = await DdlProjectConfigReader.Read(_directory, environment: null, TestContext.Current.CancellationToken);
        config.Provider.ShouldNotBeNull();
        config.Provider!.Label.ShouldBe("postgres");
        config.Provider.Version.ShouldBe(Version);
        config.State!.File.ShouldNotBeNull();

        // Assert — the sample schema parses.
        var ddl = await File.ReadAllTextAsync(Path.Combine(_directory, "schemas", "example.sql"), TestContext.Current.CancellationToken);
        DdlReader.Instance.Read(ddl).Schema.Schemas.ShouldHaveSingleItem().Tables.ShouldHaveSingleItem().Name.ShouldBe("widgets");

        // Assert — every generated file is already formatter-canonical (scaffold → fmt --check is a no-op).
        foreach (var file in new[] { "config.sql", "config.env.prod.sql", Path.Combine("schemas", "example.sql") })
        {
            var content = await File.ReadAllTextAsync(Path.Combine(_directory, file), TestContext.Current.CancellationToken);
            DdlFormatter.Instance.Format(content).ShouldBe(content, $"{file} should be formatter-canonical");
        }
    }
}
