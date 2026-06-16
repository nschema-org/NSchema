using NSchema.Commands.Init;
using NSchema.Configuration.Dsl;
using NSchema.Schema.Serialization.Ddl;

namespace NSchema.Tests.Commands.Init;

public sealed class ProjectScaffolderTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("nschema-init-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private Task<IReadOnlyList<string>> Scaffold(bool force = false) =>
        ProjectScaffolder.Scaffold(_directory, force, TestContext.Current.CancellationToken);

    [Fact]
    public async Task Scaffold_CreatesConfigAndSqlSample()
    {
        var created = await Scaffold();

        created.ShouldBe(["config.sql", Path.Combine("schemas", "example.sql")]);
        File.Exists(Path.Combine(_directory, "config.sql")).ShouldBeTrue();
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

        var config = await DslProjectConfigReader.Read(_directory, TestContext.Current.CancellationToken);
        config.Provider!.Postgres.ShouldNotBeNull();
        config.State!.File.ShouldNotBeNull();
        config.State.File!.Path.ShouldBe("./nschema.state.json");
    }

    [Fact]
    public async Task Scaffold_SampleSchema_RoundTripsThroughTheSerializer()
    {
        await Scaffold();

        await using var stream = File.OpenRead(Path.Combine(_directory, "schemas", "example.sql"));
        var schema = await DdlSchemaSerializer.Instance.Read(stream, TestContext.Current.CancellationToken);

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
}
