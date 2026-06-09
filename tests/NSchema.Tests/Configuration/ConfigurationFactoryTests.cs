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
    public void Load_HonorsDirectory_ForConfigDiscovery()
    {
        // Arrange — a project whose nschema.json lives in its own directory, not the shell's.
        File.WriteAllText(Path.Combine(_projectDirectory, "nschema.json"), """{ "state": { "file": { "path": "./custom.state.json" } } }""");
        var parseResult = RootCommand.Create().Parse(["plan", "--directory", _projectDirectory]);

        // Act
        var config = ConfigurationFactory.Load<PlanConfiguration>(parseResult);

        // Assert — the config was discovered under --directory (an empty config would have left state unset).
        config.State.File!.Path.ShouldBe("./custom.state.json");
    }
}
