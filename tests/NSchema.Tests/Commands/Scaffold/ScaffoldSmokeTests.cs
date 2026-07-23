using NSchema.Commands.Scaffold;
using NSchema.Configuration;
using NSchema.Configuration.Model;
using NSchema.Configuration.Plugins;
using NSchema.Plugins;
using NSchema.Project.Nsql;
using NSchema.Project.Nsql.Syntax.Tables;

namespace NSchema.Tests.Commands.Scaffold;

/// <summary>
/// End-to-end smoke test of the plugin-driven scaffold: loads the REAL published <c>NSchema.Postgres</c> plugin over
/// the ALC boundary, renders its config statement and sample schema, composes a project with
/// <see cref="ProjectScaffolder"/> (which authors the <c>PLUGIN</c> declaration), and asserts the result parses,
/// round-trips through the config reader, and is already formatter-canonical (so <c>scaffold</c> followed by
/// <c>fmt --check</c> is a no-op). Pins an exact version so it resolves from the cache without a feed; the floating
/// resolution is covered by <c>PluginLoaderTests</c>. Requires the .NET SDK and network access (it may restore the
/// plugin).
/// </summary>
public sealed class ScaffoldSmokeTests : IDisposable
{
    private const string Version = "5.0.0-alpha.5";

    private readonly string _directory = Directory.CreateTempSubdirectory("nschema-scaffold-smoke-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public async Task Scaffold_WithRealPostgresPlugin_ProducesAValidFormattedProject()
    {
        // Arrange — load the real plugin and render exactly what the scaffold command would.
        var plugin = new PluginLoader().Load(new PackageId("NSchema.Postgres"), SemanticVersion.Parse(Version))
            .Require()
            .OfType<INSchemaDatabasePlugin>()
            .Single();
        var providerBlock = ScaffoldCommand.Render(plugin.GetScaffoldTemplate(new ScaffoldContext()));
        var sampleSchema = plugin.GetSampleSchema();

        // Act — compose the project (file state store, like the default `nschema scaffold`).
        await ProjectScaffolder.Scaffold(_directory, force: false, "[5.0,6.0)", [("postgres", "NSchema.Postgres", Version)],
            providerBlock, sampleSchema, statePlugin: null, TestContext.Current.CancellationToken);
        await LockFileManager.Write(ProjectConfigurationReader.LockFilePath(_directory),
            new LockFile([new LockedPlugin { Source = new PackageId("NSchema.Postgres"), Version = SemanticVersion.Parse(Version) }]), TestContext.Current.CancellationToken);

        // Assert — the generated config round-trips, pinning the resolved version.
        var config = await ProjectConfigurationReader.Read(_directory, environment: null, TestContext.Current.CancellationToken);
        config.Database.ShouldNotBeNull();
        config.Database!.Label.ShouldBe("postgres");
        config.Database.Version.ToString().ShouldBe(Version);
        config.State!.File.ShouldNotBeNull();

        // Assert — the sample schema parses.
        var ddl = await File.ReadAllTextAsync(Path.Combine(_directory, "schemas", "example.sql"), TestContext.Current.CancellationToken);
        var document = NsqlReader.Read(ddl);
        document.IsSuccess.ShouldBeTrue();
        document.Require().Statements.OfType<CreateTableStatement>().ShouldHaveSingleItem().Name.Name.Value.ShouldBe("widgets");

        // Assert — every generated file is already formatter-canonical (scaffold → fmt --check is a no-op).
        foreach (var file in new[] { "config.env.sql", "config.env.prod.sql", Path.Combine("schemas", "example.sql") })
        {
            var content = await File.ReadAllTextAsync(Path.Combine(_directory, file), TestContext.Current.CancellationToken);
            NsqlWriter.Format(content).Require().ShouldBe(content, $"{file} should be formatter-canonical");
        }
    }
}
