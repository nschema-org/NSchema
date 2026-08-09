using NSchema.Commands.New;
using NSchema.Configuration;
using NSchema.Configuration.Domain;
using NSchema.Configuration.Plugins;
using NSchema.Model.Columns;
using NSchema.Model.Schemas;
using NSchema.Model.Tables;
using NSchema.Project.Nsql;
using NSchema.Project.Nsql.Syntax.Settings;
using NSchema.Project.Nsql.Syntax.Tables;

namespace NSchema.Tests.Commands.New;

/// <summary>
/// <see cref="ProjectScaffolder"/> is pure composition: given the plugins' documents and a sample schema it lays out
/// the project files (authoring the <c>PLUGIN</c> declarations and supplying the built-in file state store). These
/// tests pin that composition without loading real plugins — the live plugin resolution lives in <c>NewCommand</c> and
/// is exercised end-to-end by the smoke test.
/// </summary>
public sealed class ProjectScaffolderTests : IDisposable
{
    private static readonly IReadOnlyList<ResolvedPlugin> Plugins = [Plugin("postgres", "NSchema.Postgres")];

    private static ResolvedPlugin Plugin(string label, string package) =>
        new(new PluginLabel(label), new PackageId(package), SemanticVersion.Parse("5.0.0-test"));

    private static readonly NsqlDocument ProviderDocument =
        new([SettingsStatement.Database("postgres").WithSetting("connection_string", string.Empty)]);

    private static readonly NsqlDocument SampleSchema = NsqlDocument.From(new Model.Database
    {
        Schemas =
        [
            new Schema
            {
                Name = "app",
                Tables = { new Table { Name = "widgets", Columns = { new Column { Name = "id", Type = SqlType.BigInt } } } },
            },
        ],
    });

    private static NsqlDocument S3Document(string key) =>
        new([SettingsStatement.State("s3").WithSetting("bucket", "my-nschema-state").WithSetting("key", key)]);

    private readonly string _directory = Directory.CreateTempSubdirectory("nschema-scaffold-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    // A range covering the running engine major, so the generated config round-trips through the reader's validation.
    private const string EngineRequirement = "[5.0,6.0)";

    private async Task<IReadOnlyList<string>> Scaffold(
        bool force = false,
        NsqlDocument? databaseOverlay = null,
        NsqlDocument? state = null,
        NsqlDocument? stateOverlay = null,
        IReadOnlyList<ResolvedPlugin>? plugins = null,
        NsqlDocument? sampleSchema = null) =>
        (await ScaffoldResult(force, databaseOverlay, state, stateOverlay, plugins, sampleSchema)).Require();

    private Task<Result<IReadOnlyList<string>>> ScaffoldResult(
        bool force = false,
        NsqlDocument? databaseOverlay = null,
        NsqlDocument? state = null,
        NsqlDocument? stateOverlay = null,
        IReadOnlyList<ResolvedPlugin>? plugins = null,
        NsqlDocument? sampleSchema = null) =>
        ProjectScaffolder.Scaffold(
            _directory,
            force,
            new ProjectTemplate
            {
                EngineRequirement = EngineRequirement,
                Plugins = plugins ?? Plugins,
                Database = ProviderDocument,
                DatabaseOverlay = databaseOverlay ?? NsqlDocument.Empty,
                State = state ?? ProjectScaffolder.FileState,
                StateOverlay = stateOverlay ?? ProjectScaffolder.FileStateOverlay,
                Schema = sampleSchema ?? SampleSchema,
            },
            TestContext.Current.CancellationToken);

    private Task<string> ReadAsync(string relativePath) =>
        File.ReadAllTextAsync(Path.Combine(_directory, relativePath), TestContext.Current.CancellationToken);

    // The generated config carries exact pins; a read requires them locked (what the scaffold command's init step does).
    private Task WriteLock(params (string Source, string Version)[] plugins) =>
        LockFileManager.Write(
            ProjectConfigurationReader.LockFilePath(_directory),
            new LockFile([.. plugins.Select(p => new LockedPlugin { Source = new PackageId(p.Source), Version = SemanticVersion.Parse(p.Version) })]),
            TestContext.Current.CancellationToken);

    [Fact]
    public async Task Scaffold_CreatesConfigurationOverlayAndSample()
    {
        // Act
        var created = await Scaffold();

        // Assert
        created.ShouldBe(["config.sql", "config.env.prod.sql", Path.Combine("schemas", "example.sql")]);
        File.Exists(Path.Combine(_directory, "config.sql")).ShouldBeTrue();
        File.Exists(Path.Combine(_directory, "config.env.prod.sql")).ShouldBeTrue();
        File.Exists(Path.Combine(_directory, "schemas", "example.sql")).ShouldBeTrue();
    }

    [Fact]
    public async Task Scaffold_Configuration_ContainsPluginDeclarationDatabaseStatementAndBuiltInFileStore()
    {
        // Act
        await Scaffold();

        // Assert
        var config = await ReadAsync("config.sql");
        config.ShouldContain("ENGINE (");
        config.ShouldContain("version = '[5.0,6.0)'");
        config.ShouldContain("PLUGIN postgres");
        config.ShouldContain("source = 'NSchema.Postgres'");
        config.ShouldContain("version = '5.0.0-test'");
        config.ShouldContain("DATABASE postgres");
        config.ShouldContain("STATE file"); // the built-in default state store, owned by the CLI
    }

    [Fact]
    public async Task Scaffold_Sample_ContainsTheProviderSchema()
    {
        // Act
        await Scaffold();

        // Assert
        (await ReadAsync(Path.Combine("schemas", "example.sql"))).ShouldContain("CREATE TABLE app.widgets");
    }

    [Fact]
    public async Task Scaffold_WithPluginBackend_WritesItsStatementsInsteadOfTheFileStore()
    {
        // Act
        await Scaffold(
            state: S3Document("nschema.state.json"),
            stateOverlay: S3Document("prod/nschema.state.json"),
            plugins: [Plugin("postgres", "NSchema.Postgres"), Plugin("s3", "NSchema.Aws")]);

        // Assert
        var config = await ReadAsync("config.sql");
        config.ShouldContain("PLUGIN s3");
        config.ShouldContain("STATE s3");
        config.ShouldNotContain("STATE file");
        (await ReadAsync("config.env.prod.sql")).ShouldContain("key = 'prod/nschema.state.json'");
    }

    [Fact]
    public async Task Scaffold_GeneratedConfiguration_RoundTripsThroughTheReader()
    {
        // Arrange
        await Scaffold();
        await WriteLock(("NSchema.Postgres", "5.0.0-test"));

        // Act
        var config = (await ProjectConfigurationReader.Read(_directory, environment: null, TestContext.Current.CancellationToken)).Require();

        // Assert
        config.Database.ShouldNotBeNull();
        config.Database!.Label.ShouldBe("postgres");
        config.Database.Version!.ToString().ShouldBe("5.0.0-test");
        config.State!.File.ShouldNotBeNull();
        config.State.File!.Path.ShouldBe("./nschema.state.json");
    }

    [Fact]
    public async Task Scaffold_GeneratedOverlay_RoundTripsThroughTheReader()
    {
        // Arrange
        await Scaffold(
            state: S3Document("nschema.state.json"),
            stateOverlay: S3Document("prod/nschema.state.json"),
            plugins: [Plugin("postgres", "NSchema.Postgres"), Plugin("s3", "NSchema.Aws")]);
        await WriteLock(("NSchema.Postgres", "5.0.0-test"), ("NSchema.Aws", "5.0.0-test"));

        // Act
        var config = (await ProjectConfigurationReader.Read(_directory, environment: "prod", TestContext.Current.CancellationToken)).Require();

        // Assert
        config.State!.Plugin.ShouldNotBeNull();
        config.State.Plugin!.Label.ShouldBe("s3");
    }

    [Fact]
    public async Task Scaffold_SampleSchema_RoundTripsThroughTheReader()
    {
        // Arrange
        await Scaffold();

        // Act
        var ddl = await ReadAsync(Path.Combine("schemas", "example.sql"));
        var document = NsqlReader.Read(ddl);

        // Assert
        document.IsSuccess.ShouldBeTrue();
        var table = document.Require().Statements.OfType<CreateTableStatement>().ShouldHaveSingleItem();
        table.Name.Name.Value.ShouldBe("widgets");
    }

    [Fact]
    public async Task Scaffold_TheFileHeader_LeadsTheFile_SeparatedFromTheFirstStatement()
    {
        // Arrange
        await Scaffold();

        // Act
        var lines = (await ReadAsync("config.sql")).Split('\n');

        // Assert — a header introduces the file as plain comment lines above a blank line, not as a doc-comment
        // (which the language would read as ENGINE's catalog comment).
        lines[0].ShouldStartWith("-- NSchema project configuration");
        lines[0].ShouldNotStartWith("---");
        var engine = Array.FindIndex(lines, line => line.StartsWith("ENGINE", StringComparison.Ordinal));
        lines[engine - 1].ShouldBeEmpty();
        lines[engine - 2].ShouldStartWith("--");
    }

    [Fact]
    public async Task Scaffold_TheFileHeader_SitsAboveAStatementsOwnDocComment()
    {
        // Arrange — a plugin's statement may carry its own doc-comment, which leads the statement; the file header
        // has to come before that rather than underneath it.
        var documented = new NsqlDocument([
            SettingsStatement.State("s3").WithSetting("key", "prod/nschema.state.json")
                .WithDocComment("Credentials come from the AWS chain."),
        ]);

        // Act
        await Scaffold(
            state: S3Document("nschema.state.json"),
            stateOverlay: documented,
            plugins: [Plugin("postgres", "NSchema.Postgres"), Plugin("s3", "NSchema.Aws")]);

        // Assert
        var overlay = await ReadAsync("config.env.prod.sql");
        overlay.IndexOf("-- Overlay for", StringComparison.Ordinal)
            .ShouldBeLessThan(overlay.IndexOf("--- Credentials", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Scaffold_TheConfigurationItComposes_IsFormatterCanonical()
    {
        // Arrange — the ENGINE, PLUGIN and built-in STATE statements are the scaffolder's own, composed through the
        // writer, so `new` followed by `format --check` is a no-op.
        await Scaffold();

        // Act
        var config = await ReadAsync("config.sql");
        var overlay = await ReadAsync("config.env.prod.sql");

        // Assert
        NsqlWriter.Format(config).Require().ShouldBe(config);
        NsqlWriter.Format(overlay).Require().ShouldBe(overlay);
    }

    [Fact]
    public async Task Scaffold_TheSampleItWrites_IsFormatterCanonical()
    {
        // Arrange — the sample arrives as a document, so the writer decides its layout, not the provider.
        await Scaffold();

        // Act
        var written = await ReadAsync(Path.Combine("schemas", "example.sql"));

        // Assert
        NsqlWriter.Format(written).Require().ShouldBe(written);
    }

    [Fact]
    public async Task Scaffold_Fails_WhenDirectoryNotEmpty_AndNotForced()
    {
        // Arrange
        await File.WriteAllTextAsync(Path.Combine(_directory, "existing.sql"), "CREATE SCHEMA app;", TestContext.Current.CancellationToken);

        // Act
        var result = await ScaffoldResult(force: false);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().Message.ShouldContain("--force");
    }

    [Fact]
    public async Task Scaffold_Overwrites_WhenForced()
    {
        // Arrange
        await File.WriteAllTextAsync(Path.Combine(_directory, "existing.sql"), "CREATE SCHEMA app;", TestContext.Current.CancellationToken);

        // Act & Assert
        await Should.NotThrowAsync(() => Scaffold(force: true));
        File.Exists(Path.Combine(_directory, "config.sql")).ShouldBeTrue();
    }
}
