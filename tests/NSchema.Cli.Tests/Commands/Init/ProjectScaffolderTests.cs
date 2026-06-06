using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using NSchema.Cli.Commands.Init;
using NSchema.Cli.Configuration;
using NSchema.Cli.Configuration.Schema;
using NSchema.Resolution;
using NSchema.Schema.Serialization;

namespace NSchema.Cli.Tests.Commands.Init;

public sealed class ProjectScaffolderTests : IDisposable
{
    private readonly ProjectScaffolder _sut = new();
    private readonly string _directory = Directory.CreateTempSubdirectory("nschema-init-").FullName;
    private readonly NSchemaApplication _app = CliApplicationBuilder.Create().Build();

    private IKeyedResolver<ISchemaDocumentSerializer> Serializers => _app.Services.GetRequiredService<IKeyedResolver<ISchemaDocumentSerializer>>();

    public void Dispose()
    {
        _app.Dispose();
        Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public async Task Scaffold_CreatesConfigAndSampleSchema()
    {
        // Act
        var created = await _sut.Scaffold(_directory, SchemaFormat.Yaml, force: false, Serializers, TestContext.Current.CancellationToken);

        // Assert
        created.ShouldBe(["nschema.json", Path.Combine("schemas", "example.yaml")]);
        File.Exists(Path.Combine(_directory, "nschema.json")).ShouldBeTrue();
        File.Exists(Path.Combine(_directory, "schemas", "example.yaml")).ShouldBeTrue();
    }

    [Fact]
    public async Task Scaffold_GeneratedConfig_RoundTripsThroughTheLoader()
    {
        // Act
        await _sut.Scaffold(_directory, SchemaFormat.Yaml, force: false, Serializers, TestContext.Current.CancellationToken);

        // Assert
        var config = LoadGeneratedConfig();
        config.Provider.Postgres.ShouldNotBeNull();
        config.State.File.ShouldNotBeNull();
        config.State.File!.Path.ShouldBe("./nschema.state.json");
        config.Schema.Directory.ShouldBe("./schemas");
        config.Schema.Format.ShouldBe(SchemaFormat.Yaml);
    }

    [Fact]
    public async Task Scaffold_GeneratedConfig_OmitsUnsetDefaults()
    {
        // Act
        await _sut.Scaffold(_directory, SchemaFormat.Yaml, force: false, Serializers, TestContext.Current.CancellationToken);

        // Assert
        var json = File.ReadAllText(Path.Combine(_directory, "nschema.json"));
        json.ShouldNotContain("autoApprove");
        json.ShouldNotContain("scope");
    }

    [Fact]
    public async Task Scaffold_JsonFormat_WritesJsonSampleAndConfig()
    {
        // Act
        await _sut.Scaffold(_directory, SchemaFormat.Json, force: false, Serializers, TestContext.Current.CancellationToken);

        // Assert
        File.Exists(Path.Combine(_directory, "schemas", "example.json")).ShouldBeTrue();
        LoadGeneratedConfig().Schema.Format.ShouldBe(SchemaFormat.Json);
    }

    [Fact]
    public async Task Scaffold_SampleSchema_RoundTripsThroughTheSerializer()
    {
        // Arrange
        await _sut.Scaffold(_directory, SchemaFormat.Yaml, force: false, Serializers, TestContext.Current.CancellationToken);

        // Act
        await using var stream = File.OpenRead(Path.Combine(_directory, "schemas", "example.yaml"));
        var schema = await Serializers.Resolve("yaml").Read(stream, TestContext.Current.CancellationToken);

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
        var act = () => _sut.Scaffold(_directory, SchemaFormat.Yaml, force: false, Serializers, TestContext.Current.CancellationToken);

        // Assert
        (await Should.ThrowAsync<InvalidOperationException>(act)).Message.ShouldContain("--force");
    }

    [Fact]
    public async Task Scaffold_Overwrites_WhenForced()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_directory, "nschema.json"), "stale");

        // Act
        var act = () => _sut.Scaffold(_directory, SchemaFormat.Yaml, force: true, Serializers, TestContext.Current.CancellationToken);

        // Assert
        await Should.NotThrowAsync(act);
        LoadGeneratedConfig().Provider.Postgres.ShouldNotBeNull();
    }

    private NSchemaConfiguration LoadGeneratedConfig()
    {
        var json = File.ReadAllText(Path.Combine(_directory, "nschema.json"));
        return JsonSerializer.Deserialize<NSchemaConfiguration>(json, NSchemaConfigurationFactory.JsonOptions)!;
    }
}
