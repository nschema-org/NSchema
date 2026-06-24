using NSchema.Commands.Init;
using NSchema.Configuration.Ddl;
using NSchema.Schema.Ddl;

namespace NSchema.Tests.Commands.Init;

public sealed class ProjectScaffolderTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("nschema-init-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private Task<IReadOnlyList<string>> Scaffold(bool force = false) =>
        ProjectScaffolder.Scaffold(_directory, force, cancellationToken: TestContext.Current.CancellationToken);

    private Task<IReadOnlyList<string>> Scaffold(ProviderKind provider, BackendKind backend) =>
        ProjectScaffolder.Scaffold(_directory, force: false, provider, backend, TestContext.Current.CancellationToken);

    [Fact]
    public async Task Scaffold_CreatesConfigAndSqlSample()
    {
        var created = await Scaffold();

        created.ShouldBe(["config.sql", "config.env.prod.sql", Path.Combine("schemas", "example.sql")]);
        File.Exists(Path.Combine(_directory, "config.sql")).ShouldBeTrue();
        File.Exists(Path.Combine(_directory, "config.env.prod.sql")).ShouldBeTrue();
        File.Exists(Path.Combine(_directory, "schemas", "example.sql")).ShouldBeTrue();
    }

    [Fact]
    public async Task Scaffold_SqlSample_ContainsDdl()
    {
        await Scaffold();

        var sample = await File.ReadAllTextAsync(Path.Combine(_directory, "schemas", "example.sql"), TestContext.Current.CancellationToken);
        sample.ShouldContain("CREATE TABLE app.widgets");
    }

    [Fact]
    public async Task Scaffold_Config_ContainsProviderAndBackendBlocks()
    {
        await Scaffold();

        var config = await File.ReadAllTextAsync(Path.Combine(_directory, "config.sql"), TestContext.Current.CancellationToken);
        config.ShouldContain("PROVIDER postgres");
        config.ShouldContain("BACKEND file");
    }

    [Fact]
    public async Task Scaffold_GeneratedConfig_RoundTripsThroughTheReader()
    {
        await Scaffold();

        var config = await DdlProjectConfigReader.Read(_directory, environment: null, TestContext.Current.CancellationToken);
        config.Provider!.Postgres.ShouldNotBeNull();
        config.State!.File.ShouldNotBeNull();
        config.State.File!.Path.ShouldBe("./nschema.state.json");
    }

    [Fact]
    public async Task Scaffold_SampleSchema_RoundTripsThroughTheReader()
    {
        await Scaffold();

        var ddl = await File.ReadAllTextAsync(Path.Combine(_directory, "schemas", "example.sql"), TestContext.Current.CancellationToken);
        var schema = DdlReader.Instance.Read(ddl).Schema;

        var table = schema.Schemas.ShouldHaveSingleItem().Tables.ShouldHaveSingleItem();
        table.Name.ShouldBe("widgets");
        table.PrimaryKey!.ColumnNames.ShouldBe(["id"]);
        table.Columns.Select(c => c.Name).ShouldBe(["id", "name"]);
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

    // InlineData carries the enum names as strings: the enums are internal, so a public test method can't name them in
    // its signature. They're parsed back to the internal enums in the body, where internal types are fine.
    [Theory]
    [InlineData("postgres", "file")]
    [InlineData("postgres", "s3")]
    [InlineData("sqlite", "file")]
    [InlineData("sqlite", "s3")]
    [InlineData("sqlserver", "file")]
    [InlineData("sqlserver", "s3")]
    public async Task Scaffold_GeneratedConfig_RoundTripsForEveryProviderBackendCombination(string providerName, string backendName)
    {
        var provider = Enum.Parse<ProviderKind>(providerName, ignoreCase: true);
        var backend = Enum.Parse<BackendKind>(backendName, ignoreCase: true);

        await Scaffold(provider, backend);

        var config = await DdlProjectConfigReader.Read(_directory, environment: null, TestContext.Current.CancellationToken);

        // Exactly the selected provider section is populated.
        config.Provider.ShouldNotBeNull();
        config.Provider!.ConfiguredSectionCount.ShouldBe(1);
        (provider switch
        {
            ProviderKind.Postgres => config.Provider.Postgres is not null,
            ProviderKind.Sqlite => config.Provider.Sqlite is not null,
            ProviderKind.SqlServer => config.Provider.SqlServer is not null,
            _ => false,
        }).ShouldBeTrue();

        // Exactly the selected backend section is populated.
        config.State.ShouldNotBeNull();
        config.State!.ConfiguredSectionCount.ShouldBe(1);
        (backend switch
        {
            BackendKind.File => config.State.File is not null,
            BackendKind.S3 => config.State.S3 is not null,
            _ => false,
        }).ShouldBeTrue();
    }

    [Theory]
    [InlineData("postgres", "app")]
    [InlineData("sqlserver", "app")]
    [InlineData("sqlite", "main")]
    public async Task Scaffold_SampleSchema_TargetsTheProvidersSchema_AndRoundTrips(string providerName, string expectedSchema)
    {
        await Scaffold(Enum.Parse<ProviderKind>(providerName, ignoreCase: true), BackendKind.File);

        var ddl = await File.ReadAllTextAsync(Path.Combine(_directory, "schemas", "example.sql"), TestContext.Current.CancellationToken);
        var schema = DdlReader.Instance.Read(ddl).Schema;

        var definition = schema.Schemas.ShouldHaveSingleItem();
        definition.Name.ShouldBe(expectedSchema);
        definition.Tables.ShouldHaveSingleItem().Name.ShouldBe("widgets");
    }
}
