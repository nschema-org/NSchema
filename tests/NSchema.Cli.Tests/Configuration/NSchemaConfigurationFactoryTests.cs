using NSchema.Cli.Commands;
using NSchema.Cli.Configuration;

// Configuration resolution reads process-global environment variables, so keep tests from racing on them.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace NSchema.Cli.Tests.Configuration;

public sealed class NSchemaConfigurationFactoryTests : IDisposable
{
    private static readonly string[] _managedEnvironmentVariables =
    [
        "NSCHEMA_PROVIDER",
        "NSCHEMA_CONNECTION_STRING",
        "NSCHEMA_STATE_TYPE",
        "NSCHEMA_STATE_CONNECTION_STRING",
        "NSCHEMA_DESTRUCTIVE_ACTION_POLICY",
    ];

    private readonly Dictionary<string, string?> _environmentSnapshot = [];
    private readonly List<string> _temporaryFiles = [];

    public NSchemaConfigurationFactoryTests()
    {
        // Snapshot and clear the recognised variables so each test is hermetic regardless of the ambient environment.
        foreach (var name in _managedEnvironmentVariables)
        {
            _environmentSnapshot[name] = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, null);
        }
    }

    public void Dispose()
    {
        foreach (var (name, value) in _environmentSnapshot)
        {
            Environment.SetEnvironmentVariable(name, value);
        }

        foreach (var file in _temporaryFiles)
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void UsesConfigFile_WhenNotOverridden()
    {
        // Arrange
        var config = ConfigFile("""{ "provider": { "connectionString": "from-file" } }""");

        // Act
        var result = Create("--config", config);

        // Assert
        result.Provider.ConnectionString.ShouldBe("from-file");
    }

    [Fact]
    public void CommandLine_OverridesConfigFile()
    {
        // Arrange
        var config = ConfigFile("""{ "provider": { "connectionString": "from-file" } }""");

        // Act
        var result = Create("--config", config, "--connection-string", "from-cli");

        // Assert
        result.Provider.ConnectionString.ShouldBe("from-cli");
    }

    [Fact]
    public void Environment_OverridesConfigFile()
    {
        // Arrange
        Environment.SetEnvironmentVariable("NSCHEMA_CONNECTION_STRING", "from-env");
        var config = ConfigFile("""{ "provider": { "connectionString": "from-file" } }""");

        // Act
        var result = Create("--config", config);

        // Assert
        result.Provider.ConnectionString.ShouldBe("from-env");
    }

    [Fact]
    public void CommandLine_OverridesEnvironment()
    {
        // Arrange
        Environment.SetEnvironmentVariable("NSCHEMA_CONNECTION_STRING", "from-env");

        // Act
        var result = Create("--connection-string", "from-cli");

        // Assert
        result.Provider.ConnectionString.ShouldBe("from-cli");
    }

    [Fact]
    public void AutoApprove_ConfigValuePreserved_WhenFlagNotPassed()
    {
        // Arrange
        // The flag defaults to false; an unspecified flag must not clobber a true value from the config file.
        var config = ConfigFile("""{ "autoApprove": true }""");

        // Act
        var result = Create("apply", "--config", config);

        // Assert
        result.AutoApprove.ShouldBeTrue();
    }

    [Fact]
    public void AutoApprove_Flag_OverridesConfig()
    {
        // Arrange
        var config = ConfigFile("""{ "autoApprove": false }""");

        // Act
        var result = Create("apply", "--config", config, "--auto-approve");

        // Assert
        result.AutoApprove.ShouldBeTrue();
    }

    [Fact]
    public void Scope_CommandLineReplacesConfigList()
    {
        // Arrange
        // The configuration binder merges lists by index across providers; the override must replace, not merge.
        var config = ConfigFile("""{ "scope": ["a", "b", "c"] }""");

        // Act
        var result = Create("plan", "--config", config, "--scope", "only");

        // Assert
        result.Scope.ShouldBe(["only"]);
    }

    [Fact]
    public void StateFile_SetsFileTypeAndConnectionString()
    {
        // Act
        var result = Create("plan", "--state-file", "./state.json");

        // Assert
        result.State.Type.ShouldBe(StateType.File);
        result.State.ConnectionString.ShouldBe("./state.json");
    }

    [Fact]
    public void SchemaDirectory_BoundFromDirKey()
    {
        // Arrange
        var config = ConfigFile("""{ "schema": { "dir": "./src" } }""");

        // Act
        var result = Create("--config", config);

        // Assert
        result.Schema.Directory.ShouldBe("./src");
    }

    [Fact]
    public void Provider_ParsedAsEnum()
    {
        // Act
        var result = Create("--provider", "postgres");

        // Assert
        result.Provider.Type.ShouldBe(ProviderType.Postgres);
    }

    [Fact]
    public void StateType_BoundFromEnvironment()
    {
        // Arrange
        Environment.SetEnvironmentVariable("NSCHEMA_STATE_TYPE", "s3");

        // Act
        var result = Create();

        // Assert
        result.State.Type.ShouldBe(StateType.S3);
    }

    // NSchemaConfigurationFactory is static, so the closest thing to a "_sut" is this invocation helper.
    private static NSchemaConfiguration Create(params string[] args)
        => NSchemaConfigurationFactory.Create(RootCommand.Create().Parse(args));

    private string ConfigFile(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), $"nschema-test-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        _temporaryFiles.Add(path);
        return path;
    }
}
