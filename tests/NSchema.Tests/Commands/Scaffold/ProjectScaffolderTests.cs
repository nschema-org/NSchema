using NSchema.Commands.Scaffold;
using NSchema.Configuration.Ddl;
using NSchema.Schema.Ddl;

namespace NSchema.Tests.Commands.Scaffold;

/// <summary>
/// <see cref="ProjectScaffolder"/> is pure composition: given plugin-rendered config blocks and a sample schema it
/// lays out the project files (and supplies the built-in file backend). These tests pin that composition without
/// loading real plugins — the live plugin resolution lives in <c>ScaffoldCommand</c> and is exercised end-to-end by
/// the plugin-loader/smoke tests.
/// </summary>
public sealed class ProjectScaffolderTests : IDisposable
{
    private const string ProviderBlock =
        """
        PROVIDER postgres (
          version           = '4.0.0-test',
          connection_string = ''
        );
        """;

    private const string SampleSchema =
        """
        CREATE SCHEMA app;

        CREATE TABLE app.widgets (
          id   bigint NOT NULL,
          name text,
          CONSTRAINT widgets_pkey PRIMARY KEY (id)
        );
        """;

    private const string S3BackendBlock =
        """
        BACKEND s3 (
          version = '4.0.0-test',
          bucket  = 'my-nschema-state',
          key     = 'nschema.state.json'
        );
        """;

    private const string S3OverlayBlock =
        """
        BACKEND s3 (
          version = '4.0.0-test',
          bucket  = 'my-nschema-state',
          key     = 'prod/nschema.state.json'
        );
        """;

    private readonly string _directory = Directory.CreateTempSubdirectory("nschema-scaffold-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private Task<IReadOnlyList<string>> Scaffold(bool force = false, (string Base, string Overlay)? pluginBackend = null) =>
        ProjectScaffolder.Scaffold(_directory, force, ProviderBlock, SampleSchema, pluginBackend, TestContext.Current.CancellationToken);

    private Task<string> ReadAsync(string relativePath) =>
        File.ReadAllTextAsync(Path.Combine(_directory, relativePath), TestContext.Current.CancellationToken);

    [Fact]
    public async Task Scaffold_CreatesConfigOverlayAndSample()
    {
        var created = await Scaffold();

        created.ShouldBe(["config.sql", "config.env.prod.sql", Path.Combine("schemas", "example.sql")]);
        File.Exists(Path.Combine(_directory, "config.sql")).ShouldBeTrue();
        File.Exists(Path.Combine(_directory, "config.env.prod.sql")).ShouldBeTrue();
        File.Exists(Path.Combine(_directory, "schemas", "example.sql")).ShouldBeTrue();
    }

    [Fact]
    public async Task Scaffold_Config_ContainsProviderBlockAndBuiltInFileBackend()
    {
        await Scaffold();

        var config = await ReadAsync("config.sql");
        config.ShouldContain("PROVIDER postgres");
        config.ShouldContain("BACKEND file"); // the built-in default backend, owned by the CLI
    }

    [Fact]
    public async Task Scaffold_Sample_ContainsTheProviderSchema()
    {
        await Scaffold();

        (await ReadAsync(Path.Combine("schemas", "example.sql"))).ShouldContain("CREATE TABLE app.widgets");
    }

    [Fact]
    public async Task Scaffold_WithPluginBackend_WritesItsBlocksInsteadOfTheFileBackend()
    {
        await Scaffold(pluginBackend: (S3BackendBlock, S3OverlayBlock));

        var config = await ReadAsync("config.sql");
        config.ShouldContain("BACKEND s3");
        config.ShouldNotContain("BACKEND file");
        (await ReadAsync("config.env.prod.sql")).ShouldContain("key     = 'prod/nschema.state.json'");
    }

    [Fact]
    public async Task Scaffold_GeneratedConfig_RoundTripsThroughTheReader()
    {
        await Scaffold();

        var config = await DdlProjectConfigReader.Read(_directory, environment: null, TestContext.Current.CancellationToken);
        config.Provider.ShouldNotBeNull();
        config.Provider!.Label.ShouldBe("postgres");
        config.State!.File.ShouldNotBeNull();
        config.State.File!.Path.ShouldBe("./nschema.state.json");
    }

    [Fact]
    public async Task Scaffold_GeneratedOverlay_RoundTripsThroughTheReader()
    {
        await Scaffold(pluginBackend: (S3BackendBlock, S3OverlayBlock));

        var config = await DdlProjectConfigReader.Read(_directory, environment: "prod", TestContext.Current.CancellationToken);
        config.State!.Plugin.ShouldNotBeNull();
        config.State.Plugin!.Label.ShouldBe("s3");
    }

    [Fact]
    public async Task Scaffold_SampleSchema_RoundTripsThroughTheReader()
    {
        await Scaffold();

        var ddl = await ReadAsync(Path.Combine("schemas", "example.sql"));
        var schema = DdlReader.Instance.Read(ddl).Schema;

        var table = schema.Schemas.ShouldHaveSingleItem().Tables.ShouldHaveSingleItem();
        table.Name.ShouldBe("widgets");
        table.PrimaryKey!.ColumnNames.ShouldBe(["id"]);
    }

    [Fact]
    public async Task Scaffold_Throws_WhenDirectoryNotEmpty_AndNotForced()
    {
        await File.WriteAllTextAsync(Path.Combine(_directory, "existing.sql"), "CREATE SCHEMA app;", TestContext.Current.CancellationToken);

        var act = () => Scaffold(force: false);

        (await Should.ThrowAsync<InvalidOperationException>(act)).Message.ShouldContain("--force");
    }

    [Fact]
    public async Task Scaffold_Overwrites_WhenForced()
    {
        await File.WriteAllTextAsync(Path.Combine(_directory, "existing.sql"), "CREATE SCHEMA app;", TestContext.Current.CancellationToken);

        await Should.NotThrowAsync(() => Scaffold(force: true));
        File.Exists(Path.Combine(_directory, "config.sql")).ShouldBeTrue();
    }
}
