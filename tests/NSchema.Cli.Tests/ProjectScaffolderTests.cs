using System.Text.Json;
using NSchema.Cli.Configuration;
using NSchema.Cli.Configuration.Provider;
using NSchema.Cli.Configuration.Schema;
using NSchema.Cli.Configuration.State;

namespace NSchema.Cli.Tests;

public sealed class ProjectScaffolderTests : IDisposable
{
    private readonly ProjectScaffolder _sut = new();
    private readonly string _directory = Directory.CreateTempSubdirectory("nschema-init-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void Scaffold_CreatesConfigAndSampleSchema()
    {
        // Act
        var created = _sut.Scaffold(_directory, SchemaFormat.Yaml, force: false);

        // Assert
        created.ShouldBe(["nschema.json", Path.Combine("schemas", "example.yaml")]);
        File.Exists(Path.Combine(_directory, "nschema.json")).ShouldBeTrue();
        File.Exists(Path.Combine(_directory, "schemas", "example.yaml")).ShouldBeTrue();
    }

    [Fact]
    public void Scaffold_GeneratedConfig_RoundTripsThroughTheLoader()
    {
        // Act
        _sut.Scaffold(_directory, SchemaFormat.Yaml, force: false);

        // Assert
        var config = LoadGeneratedConfig();
        config.Provider.SelectedType.ShouldBe(ProviderType.Postgres);
        config.State.SelectedType.ShouldBe(StateType.File);
        config.State.File!.Path.ShouldBe("./nschema.state.json");
        config.Schema.Directory.ShouldBe("./schemas");
        config.Schema.Format.ShouldBe(SchemaFormat.Yaml);
    }

    [Fact]
    public void Scaffold_GeneratedConfig_OmitsUnsetDefaults()
    {
        // Act
        _sut.Scaffold(_directory, SchemaFormat.Yaml, force: false);

        // Assert
        var json = File.ReadAllText(Path.Combine(_directory, "nschema.json"));
        json.ShouldNotContain("autoApprove");
        json.ShouldNotContain("scope");
    }

    [Fact]
    public void Scaffold_JsonFormat_WritesJsonSampleAndConfig()
    {
        // Act
        _sut.Scaffold(_directory, SchemaFormat.Json, force: false);

        // Assert
        File.Exists(Path.Combine(_directory, "schemas", "example.json")).ShouldBeTrue();
        LoadGeneratedConfig().Schema.Format.ShouldBe(SchemaFormat.Json);
    }

    [Fact]
    public void Scaffold_Throws_WhenConfigExists_AndNotForced()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_directory, "nschema.json"), "{}");

        // Act
        var act = () => _sut.Scaffold(_directory, SchemaFormat.Yaml, force: false);

        // Assert
        Should.Throw<InvalidOperationException>(act).Message.ShouldContain("--force");
    }

    [Fact]
    public void Scaffold_Overwrites_WhenForced()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_directory, "nschema.json"), "stale");

        // Act
        var act = () => _sut.Scaffold(_directory, SchemaFormat.Yaml, force: true);

        // Assert
        Should.NotThrow(act);
        LoadGeneratedConfig().Provider.SelectedType.ShouldBe(ProviderType.Postgres);
    }

    private NSchemaConfiguration LoadGeneratedConfig()
    {
        var json = File.ReadAllText(Path.Combine(_directory, "nschema.json"));
        return JsonSerializer.Deserialize<NSchemaConfiguration>(json, NSchemaConfigurationFactory.JsonOptions)!;
    }
}
