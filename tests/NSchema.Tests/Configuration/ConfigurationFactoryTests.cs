using NSchema.Commands.Plan;
using NSchema.Configuration;
using RootCommand = NSchema.Commands.RootCommand;

namespace NSchema.Tests.Configuration;

public sealed class ConfigurationFactoryTests : IDisposable
{
    private readonly string _originalDirectory = Directory.GetCurrentDirectory();
    private readonly string _projectDirectory = Directory.CreateTempSubdirectory("nschema-cfg-").FullName;

    // Load applies --directory by changing the process working directory; restore it so tests stay hermetic.
    public void Dispose()
    {
        Directory.SetCurrentDirectory(_originalDirectory);
        Directory.Delete(_projectDirectory, recursive: true);
    }

    [Fact]
    public async Task Load_HonorsDirectory_ForConfigDiscovery()
    {
        // Arrange — a project whose config blocks live in its own directory, not the shell's.
        await File.WriteAllTextAsync(Path.Combine(_projectDirectory, "config.env.sql"), "STATE file ( path = './custom.state.json' );", TestContext.Current.CancellationToken);
        var parseResult = RootCommand.Create().Parse(["plan", "--directory", _projectDirectory]);

        // Act
        var config = await ConfigurationFactory.Load<PlanConfiguration>(parseResult, ConfigurationFactory.ResolveEnvironment(parseResult), TestContext.Current.CancellationToken);

        // Assert — the config was discovered under --directory (an empty config would have left state unset).
        config.State!.File!.Path.ShouldBe("./custom.state.json");
    }

    [Fact]
    public async Task Load_Environment_LayersOverlayOverBase()
    {
        // Base config picks the file store; the prod overlay (selected via --environment) replaces it with S3.
        await File.WriteAllTextAsync(Path.Combine(_projectDirectory, "config.env.sql"),
            "PLUGIN s3 ( source = 'NSchema.Aws', version = '5.0.0' );\nSTATE file ( path = './state.json' );", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(_projectDirectory, "config.env.prod.sql"),
            "STATE s3 ( bucket = 'prod-bucket', key = 'state.json' );", TestContext.Current.CancellationToken);
        var parseResult = RootCommand.Create().Parse(["plan", "--directory", _projectDirectory, "--environment", "prod"]);

        var config = await ConfigurationFactory.Load<PlanConfiguration>(parseResult, ConfigurationFactory.ResolveEnvironment(parseResult), TestContext.Current.CancellationToken);

        config.State!.File.ShouldBeNull();
        config.State.Plugin!.Config.Attribute("bucket")!.AsString().ShouldBe("prod-bucket");
    }
}
