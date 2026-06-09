using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using NSchema.Commands.Init;
using NSchema.Commands.Plan;
using NSchema.Configuration;
using NSchema.Resolution;
using NSchema.Schema.Serialization;

namespace NSchema.Tests.Commands.Init;

public sealed class ProjectScaffolderTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("nschema-init-").FullName;
    private readonly NSchemaApplication _app = CliApplicationBuilder.Create().Build();

    private IKeyedResolver<ISchemaSerializer> Serializers => _app.Services.GetRequiredService<IKeyedResolver<ISchemaSerializer>>();

    public void Dispose()
    {
        _app.Dispose();
        Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public async Task Scaffold_CreatesConfigAndSqlSample()
    {
        // Act
        var created = await ProjectScaffolder.Scaffold(_directory, force: false, Serializers, TestContext.Current.CancellationToken);

        // Assert
        created.ShouldBe(["nschema.json", Path.Combine("schemas", "example.sql")]);
        File.Exists(Path.Combine(_directory, "nschema.json")).ShouldBeTrue();
        File.Exists(Path.Combine(_directory, "schemas", "example.sql")).ShouldBeTrue();
    }

    [Fact]
    public async Task Scaffold_SqlSample_ContainsDdl()
    {
        // Act
        await ProjectScaffolder.Scaffold(_directory, force: false, Serializers, TestContext.Current.CancellationToken);

        // Assert
        var sample = await File.ReadAllTextAsync(Path.Combine(_directory, "schemas", "example.sql"), TestContext.Current.CancellationToken);
        sample.ShouldContain("CREATE TABLE app.widgets");
    }

    [Fact]
    public async Task Scaffold_GeneratedConfig_RoundTripsThroughTheLoader()
    {
        // Act
        await ProjectScaffolder.Scaffold(_directory, force: false, Serializers, TestContext.Current.CancellationToken);

        // Assert
        var config = LoadGeneratedConfig();
        config.Provider.Postgres.ShouldNotBeNull();
        config.State.File.ShouldNotBeNull();
        config.State.File!.Path.ShouldBe("./nschema.state.json");
        config.Schema.Directory.ShouldBe("./schemas");
    }

    [Fact]
    public async Task Scaffold_GeneratedConfig_OmitsUnsetDefaults()
    {
        // Act
        await ProjectScaffolder.Scaffold(_directory, force: false, Serializers, TestContext.Current.CancellationToken);

        // Assert
        var json = File.ReadAllText(Path.Combine(_directory, "nschema.json"));
        json.ShouldNotContain("autoApprove");
        json.ShouldNotContain("scope");
        json.ShouldNotContain("format");
    }

    [Fact]
    public async Task Scaffold_SampleSchema_RoundTripsThroughTheSerializer()
    {
        // Arrange
        await ProjectScaffolder.Scaffold(_directory, force: false, Serializers, TestContext.Current.CancellationToken);

        // Act
        await using var stream = File.OpenRead(Path.Combine(_directory, "schemas", "example.sql"));
        var schema = await Serializers.Resolve("sql").Read(stream, TestContext.Current.CancellationToken);

        // Assert
        var table = schema.Schemas.ShouldHaveSingleItem().Tables.ShouldHaveSingleItem();
        table.Name.ShouldBe("widgets");
        table.PrimaryKey!.ColumnNames.ShouldBe(["id"]);
        table.Columns.Select(c => c.Name).ShouldBe(["id", "name"]);
    }

    [Fact]
    public async Task Scaffold_Throws_WhenConfigExists_AndNotForced()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_directory, "nschema.json"), "{}");

        // Act
        var act = () => ProjectScaffolder.Scaffold(_directory, force: false, Serializers, TestContext.Current.CancellationToken);

        // Assert
        (await Should.ThrowAsync<InvalidOperationException>(act)).Message.ShouldContain("--force");
    }

    [Fact]
    public async Task Scaffold_Overwrites_WhenForced()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_directory, "nschema.json"), "stale");

        // Act
        var act = () => ProjectScaffolder.Scaffold(_directory, force: true, Serializers, TestContext.Current.CancellationToken);

        // Assert
        await Should.NotThrowAsync(act);
        LoadGeneratedConfig().Provider.Postgres.ShouldNotBeNull();
    }

    private PlanConfiguration LoadGeneratedConfig()
    {
        var json = File.ReadAllText(Path.Combine(_directory, "nschema.json"));
        return JsonSerializer.Deserialize<PlanConfiguration>(json, ConfigurationFactory.JsonOptions)!;
    }
}
