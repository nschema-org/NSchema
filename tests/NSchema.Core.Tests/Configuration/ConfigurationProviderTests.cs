using NSchema.Configuration;
using NSchema.Configuration.Domain;
using NSchema.Configuration.Engine;
using NSchema.Configuration.Plugins;
using NSchema.Project.Nsql;

namespace NSchema.Tests.Configuration;

/// <summary>
/// The configuration façade: layers in, a validated <see cref="ConfigurationDefinition"/> out. Reads each layer,
/// merges by precedence, assembles, and enforces the project's <c>ENGINE</c> assertion (engine version always,
/// host version when a host is supplied).
/// </summary>
public sealed class ConfigurationProviderTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("nschema-config-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string Write(string name, string content)
    {
        var path = Path.Combine(_root, name);
        File.WriteAllText(path, content);
        return path;
    }

    private Task<Result<ConfigurationDefinition, NsqlDiagnostic>> Load(IReadOnlyList<string> paths, SemanticVersion? hostVersion = null)
        => ConfigurationProvider.Load(paths, hostVersion, TestContext.Current.CancellationToken);

    [Fact]
    public async Task Load_ReadsAndAssembles()
    {
        // Arrange
        var plugins = Write("plugins.sql",
            """
            PLUGIN pg ( source = 'NSchema.Postgres', version = '5.0.1' );
            ENGINE ( version = '[5.0,6.0)' );
            """);
        var config = Write("config.sql",
            """
            DATABASE pg ( host = 'localhost' );
            STATE file ( path = 'state/app.nsstate' );
            """);

        // Act
        var result = await Load([plugins, config]);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Plugins.ShouldHaveSingleItem().ShouldBe(new PluginDeclaration("pg", new PackageReference { Source = "NSchema.Postgres", Version = VersionRange.Parse("5.0.1") }));
        result.Value.Engine.ShouldBe(new EngineConfiguration { Version = VersionRange.Parse("[5.0,6.0)") });
        result.Value.Database!.Value("host").ShouldBe("localhost");
        result.Value.State!.Label.ShouldBe("file");
    }

    [Fact]
    public async Task Load_UnreadableFile_IsAnError_AndTheRestStillAssembles()
    {
        // Arrange
        var config = Write("config.sql", "PLUGIN pg ( source = 'NSchema.Postgres', version = '5.0.1' );");
        var missing = Path.Combine(_root, "missing.sql");

        // Act
        var result = await Load([config, missing]);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().Message.ShouldContain("missing.sql");
        result.Value!.Plugins.ShouldHaveSingleItem().Label.ShouldBe("pg");
    }

    [Fact]
    public async Task Load_MergesReadAndAssemblyDiagnostics()
    {
        // Arrange — one file with a syntax error, another whose DATABASE names no plugin.
        var broken = Write("broken.sql", "WORKSPACE staging ( region = 'eu' );");
        var config = Write("config.sql", "DATABASE pg ( host = 'localhost' );");

        // Act
        var result = await Load([broken, config]);

        // Assert — both stages report, each finding stamped with its file.
        result.Errors.Count().ShouldBe(2);
        result.Errors.ShouldContain(d => d.File!.EndsWith("broken.sql") && d.Message.Contains("Unknown statement"));
        result.Errors.ShouldContain(d => d.File!.EndsWith("config.sql") && d.Message.Contains("no PLUGIN statement declares it"));
    }

    // ── ENGINE enforcement ────────────────────────────────────────────────────

    [Fact]
    public async Task Load_EngineVersionUnsatisfied_IsAnError()
    {
        // Arrange — this engine (Core) sits outside the required range.
        var config = Write("config.sql", "ENGINE ( version = '[4.0,5.0)' );");

        // Act / Assert
        (await Load([config])).Errors.ShouldHaveSingleItem().Message.ShouldContain("engine version");
    }

    [Fact]
    public async Task Load_HostVersionUnsatisfied_IsAnError()
    {
        // Arrange
        var config = Write("config.sql", "ENGINE ( host_version = '[5.2,6.0)' );");

        // Act / Assert — the host tool is older than the project requires.
        (await Load([config], SemanticVersion.Parse("5.0.0"))).Errors.ShouldHaveSingleItem().Message.ShouldContain("host version");
    }

    [Fact]
    public async Task Load_HostVersionSatisfied_Succeeds()
    {
        // Act
        var config = Write("config.sql", "ENGINE ( host_version = '[5.0,6.0)' );");

        // Assert
        (await Load([config], SemanticVersion.Parse("5.1.0"))).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Load_HostVersion_WithoutAHost_IsNotApplicable()
    {
        // Act
        // A host_version assertion has nothing to check when the engine is embedded directly (no host supplied).
        var config = Write("config.sql", "ENGINE ( host_version = '[5.2,6.0)' );");

        // Assert
        (await Load([config], hostVersion: null)).IsSuccess.ShouldBeTrue();
    }

    // ── Layering ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Load_HigherLayer_ReplacesTheStateSlice()
    {
        // Arrange — the base declares a file store; the overlay swaps it for a plugin-backed store, wholesale.
        var baseLayer = Write("base.sql",
            """
            PLUGIN s3 ( source = 'NSchema.Aws', version = '5.0.1' );
            STATE file ( path = './state.nsstate' );
            """);
        var overlay = Write("overlay.sql", "STATE s3 ( bucket = 'prod' );");

        // Act
        var result = await ConfigurationProvider.Load(
            [new ConfigurationLayer([baseLayer]), new ConfigurationLayer([overlay])],
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.State!.Label.ShouldBe("s3");
        result.Value.State!.Value("bucket").ShouldBe("prod");
    }
}
