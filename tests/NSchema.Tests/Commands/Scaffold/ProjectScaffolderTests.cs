using NSchema.Commands.Scaffold;
using NSchema.Configuration;
using NSchema.Project.Nsql;
using NSchema.Project.Nsql.Syntax.Tables;

namespace NSchema.Tests.Commands.Scaffold;

/// <summary>
/// <see cref="ProjectScaffolder"/> is pure composition: given plugin-rendered config statements and a sample schema
/// it lays out the project files (authoring the <c>PLUGIN</c> declarations and supplying the built-in file state
/// store). These tests pin that composition without loading real plugins — the live plugin resolution lives in
/// <c>ScaffoldCommand</c> and is exercised end-to-end by the plugin-loader/smoke tests.
/// </summary>
public sealed class ProjectScaffolderTests : IDisposable
{
    private static readonly IReadOnlyList<(string Label, string PackageId, string Version)> Plugins =
        [("postgres", "NSchema.Postgres", "5.0.0-test")];

    private const string ProviderBlock =
        """
        DATABASE postgres (
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
        STATE s3 (
          bucket = 'my-nschema-state',
          key    = 'nschema.state.json'
        );
        """;

    private const string S3OverlayBlock =
        """
        STATE s3 (
          bucket = 'my-nschema-state',
          key    = 'prod/nschema.state.json'
        );
        """;

    private readonly string _directory = Directory.CreateTempSubdirectory("nschema-scaffold-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private Task<IReadOnlyList<string>> Scaffold(
        bool force = false,
        (string Base, string Overlay)? pluginBackend = null,
        IReadOnlyList<(string Label, string PackageId, string Version)>? plugins = null) =>
        ProjectScaffolder.Scaffold(_directory, force, plugins ?? Plugins, ProviderBlock, SampleSchema, pluginBackend, TestContext.Current.CancellationToken);

    private Task<string> ReadAsync(string relativePath) =>
        File.ReadAllTextAsync(Path.Combine(_directory, relativePath), TestContext.Current.CancellationToken);

    [Fact]
    public async Task Scaffold_CreatesConfigOverlayAndSample()
    {
        var created = await Scaffold();

        created.ShouldBe(["config.env.sql", "config.env.prod.sql", Path.Combine("schemas", "example.sql")]);
        File.Exists(Path.Combine(_directory, "config.env.sql")).ShouldBeTrue();
        File.Exists(Path.Combine(_directory, "config.env.prod.sql")).ShouldBeTrue();
        File.Exists(Path.Combine(_directory, "schemas", "example.sql")).ShouldBeTrue();
    }

    [Fact]
    public async Task Scaffold_Config_ContainsPluginDeclarationDatabaseStatementAndBuiltInFileStore()
    {
        await Scaffold();

        var config = await ReadAsync("config.env.sql");
        config.ShouldContain("PLUGIN postgres");
        config.ShouldContain("source  = 'NSchema.Postgres'");
        config.ShouldContain("version = '5.0.0-test'");
        config.ShouldContain("DATABASE postgres");
        config.ShouldContain("STATE file"); // the built-in default state store, owned by the CLI
    }

    [Fact]
    public async Task Scaffold_Sample_ContainsTheProviderSchema()
    {
        await Scaffold();

        (await ReadAsync(Path.Combine("schemas", "example.sql"))).ShouldContain("CREATE TABLE app.widgets");
    }

    [Fact]
    public async Task Scaffold_WithPluginBackend_WritesItsStatementsInsteadOfTheFileStore()
    {
        await Scaffold(
            pluginBackend: (S3BackendBlock, S3OverlayBlock),
            plugins: [("postgres", "NSchema.Postgres", "5.0.0-test"), ("s3", "NSchema.Aws", "5.0.0-test")]);

        var config = await ReadAsync("config.env.sql");
        config.ShouldContain("PLUGIN s3");
        config.ShouldContain("STATE s3");
        config.ShouldNotContain("STATE file");
        (await ReadAsync("config.env.prod.sql")).ShouldContain("key    = 'prod/nschema.state.json'");
    }

    [Fact]
    public async Task Scaffold_GeneratedConfig_RoundTripsThroughTheReader()
    {
        await Scaffold();

        var config = await ProjectConfigReader.Read(_directory, environment: null, TestContext.Current.CancellationToken);
        config.Provider.ShouldNotBeNull();
        config.Provider!.Label.ShouldBe("postgres");
        config.Provider.Version.ShouldBe("5.0.0-test");
        config.State!.File.ShouldNotBeNull();
        config.State.File!.Path.ShouldBe("./nschema.state.json");
    }

    [Fact]
    public async Task Scaffold_GeneratedOverlay_RoundTripsThroughTheReader()
    {
        await Scaffold(
            pluginBackend: (S3BackendBlock, S3OverlayBlock),
            plugins: [("postgres", "NSchema.Postgres", "5.0.0-test"), ("s3", "NSchema.Aws", "5.0.0-test")]);

        var config = await ProjectConfigReader.Read(_directory, environment: "prod", TestContext.Current.CancellationToken);
        config.State!.Plugin.ShouldNotBeNull();
        config.State.Plugin!.Label.ShouldBe("s3");
    }

    [Fact]
    public async Task Scaffold_SampleSchema_RoundTripsThroughTheReader()
    {
        await Scaffold();

        var ddl = await ReadAsync(Path.Combine("schemas", "example.sql"));
        var document = NsqlReader.Read(ddl);

        document.IsSuccess.ShouldBeTrue();
        var table = document.Require().Statements.OfType<CreateTableStatement>().ShouldHaveSingleItem();
        table.Name.Name.Value.ShouldBe("widgets");
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
        File.Exists(Path.Combine(_directory, "config.env.sql")).ShouldBeTrue();
    }
}
