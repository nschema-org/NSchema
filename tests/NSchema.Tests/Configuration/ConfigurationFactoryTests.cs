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
        var config = await ConfigurationFactory.Load<PlanConfiguration>(parseResult, ConfigurationFactory.ResolveEnvironment(parseResult), TestContext.Current.CancellationToken);

        // Assert — the config was discovered under --directory (an empty config would have left state unset).
        config.State.File!.Path.ShouldBe("./custom.state.json");
    }

    [Fact]
    public async Task Load_EnvironmentVariable_OverridesDdlConnectionString()
    {
        // The DDL config block is the lowest layer; the environment variable wins.
        await File.WriteAllTextAsync(Path.Combine(_projectDirectory, "config.sql"), "PROVIDER postgres ( connection_string = 'from-ddl' );", TestContext.Current.CancellationToken);
        var parseResult = RootCommand.Create().Parse(["plan", "--directory", _projectDirectory]);

        try
        {
            Environment.SetEnvironmentVariable(EnvironmentVariables.PostgresConnectionString, "from-env");
            var config = await ConfigurationFactory.Load<PlanConfiguration>(parseResult, ConfigurationFactory.ResolveEnvironment(parseResult), TestContext.Current.CancellationToken);
            config.Provider.Postgres!.ConnectionString.ShouldBe("from-env");
        }
        finally
        {
            Environment.SetEnvironmentVariable(EnvironmentVariables.PostgresConnectionString, null);
        }
    }

    [Fact]
    public async Task Load_EnvironmentVariables_SupplyCredentialsSeparatelyFromConnectionString()
    {
        // The base connection string carries only the non-secret host; the credential env vars (as a secret store
        // would inject them) layer on top, overriding any user/password the block omitted.
        await File.WriteAllTextAsync(Path.Combine(_projectDirectory, "config.sql"), "PROVIDER postgres ( connection_string = 'host=db' );", TestContext.Current.CancellationToken);
        var parseResult = RootCommand.Create().Parse(["plan", "--directory", _projectDirectory]);

        try
        {
            Environment.SetEnvironmentVariable(EnvironmentVariables.PostgresUsername, "app");
            Environment.SetEnvironmentVariable(EnvironmentVariables.PostgresPassword, "secret");
            var config = await ConfigurationFactory.Load<PlanConfiguration>(parseResult, ConfigurationFactory.ResolveEnvironment(parseResult), TestContext.Current.CancellationToken);
            config.Provider.Postgres!.ConnectionString.ShouldBe("host=db");
            config.Provider.Postgres.Username.ShouldBe("app");
            config.Provider.Postgres.Password.ShouldBe("secret");
        }
        finally
        {
            Environment.SetEnvironmentVariable(EnvironmentVariables.PostgresUsername, null);
            Environment.SetEnvironmentVariable(EnvironmentVariables.PostgresPassword, null);
        }
    }

    [Fact]
    public async Task Load_EnvironmentVariable_OverridesDdlPassword()
    {
        // A password in the block is the lowest layer; the environment variable wins (the preferred place for secrets).
        await File.WriteAllTextAsync(Path.Combine(_projectDirectory, "config.sql"), "PROVIDER postgres ( connection_string = 'host=db', password = 'from-ddl' );", TestContext.Current.CancellationToken);
        var parseResult = RootCommand.Create().Parse(["plan", "--directory", _projectDirectory]);

        try
        {
            Environment.SetEnvironmentVariable(EnvironmentVariables.PostgresPassword, "from-env");
            var config = await ConfigurationFactory.Load<PlanConfiguration>(parseResult, ConfigurationFactory.ResolveEnvironment(parseResult), TestContext.Current.CancellationToken);
            config.Provider.Postgres!.Password.ShouldBe("from-env");
        }
        finally
        {
            Environment.SetEnvironmentVariable(EnvironmentVariables.PostgresPassword, null);
        }
    }

    [Fact]
    public async Task Load_Environment_LayersOverlayOverBase()
    {
        // Base config picks a file backend; the prod overlay (selected via --environment) replaces it with S3.
        await File.WriteAllTextAsync(Path.Combine(_projectDirectory, "config.sql"),
            "BACKEND file ( path = './state.json' );", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(_projectDirectory, "config.env.prod.sql"),
            "BACKEND s3 ( bucket = 'prod-bucket', key = 'state.json' );", TestContext.Current.CancellationToken);
        var parseResult = RootCommand.Create().Parse(["plan", "--directory", _projectDirectory, "--environment", "prod"]);

        var config = await ConfigurationFactory.Load<PlanConfiguration>(parseResult, ConfigurationFactory.ResolveEnvironment(parseResult), TestContext.Current.CancellationToken);

        config.State.File.ShouldBeNull();
        config.State.S3!.Bucket.ShouldBe("prod-bucket");
    }
}
