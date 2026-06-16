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
        await File.WriteAllTextAsync(Path.Combine(_projectDirectory, "config.sql"), "BACKEND file ( path = './custom.state.json' );", TestContext.Current.CancellationToken);
        var parseResult = RootCommand.Create().Parse(["plan", "--directory", _projectDirectory]);

        // Act
        var config = await ConfigurationFactory.Load<PlanConfiguration>(parseResult, TestContext.Current.CancellationToken);

        // Assert — the config was discovered under --directory (an empty config would have left state unset).
        config.State.File!.Path.ShouldBe("./custom.state.json");
    }

    [Fact]
    public async Task Load_EnvironmentVariable_OverridesDslConnectionString()
    {
        // The DSL config block is the lowest layer; the environment variable wins.
        await File.WriteAllTextAsync(Path.Combine(_projectDirectory, "config.sql"), "PROVIDER postgres ( connection_string = 'from-dsl' );", TestContext.Current.CancellationToken);
        var parseResult = RootCommand.Create().Parse(["plan", "--directory", _projectDirectory]);

        try
        {
            Environment.SetEnvironmentVariable(EnvironmentVariables.PostgresConnectionString, "from-env");
            var config = await ConfigurationFactory.Load<PlanConfiguration>(parseResult, TestContext.Current.CancellationToken);
            config.Provider.Postgres!.ConnectionString.ShouldBe("from-env");
        }
        finally
        {
            Environment.SetEnvironmentVariable(EnvironmentVariables.PostgresConnectionString, null);
        }
    }
}
