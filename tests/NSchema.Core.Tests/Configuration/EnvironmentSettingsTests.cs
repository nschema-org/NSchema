using NSchema.Configuration;
using NSchema.Project.Nsql;

namespace NSchema.Tests.Configuration;

/// <summary>
/// Environment overrides on a configuring statement: one rule (<c>NSCHEMA_&lt;KEYWORD&gt;_&lt;SETTING&gt;</c>) covers
/// every setting a plugin declares, so a secret can be supplied without writing it into a project file.
/// </summary>
public sealed class EnvironmentSettingsTests
{
    private const string Plugins = """
        PLUGIN pg ( source = 'NSchema.Postgres', version = '5.0.1' );
        PLUGIN s3 ( source = 'NSchema.Aws', version = '5.0.1' );
        """;

    private static ConfigurationDefinition Assemble(string source, Dictionary<string, string?> environment)
    {
        var document = NsqlReader.Read($"{Plugins}\n{source}");
        document.IsSuccess.ShouldBeTrue();

        var assembled = ConfigurationAssembler.Assemble([document.Value], environment);
        assembled.IsSuccess.ShouldBeTrue();
        return assembled.Value;
    }

    [Fact]
    public void Override_ReplacesTheWrittenSetting()
    {
        // Arrange
        var environment = new Dictionary<string, string?> { ["NSCHEMA_DATABASE_CONNECTION_STRING"] = "Host=live" };

        // Act
        var definition = Assemble("DATABASE pg ( connection_string = 'Host=localhost' );", environment);

        // Assert
        definition.Database!.Value("connection_string").ShouldBe("Host=live");
    }

    [Fact]
    public void Override_SuppliesASettingTheStatementOmits()
    {
        // Arrange — the whole point: the connection string never appears in a committed file.
        var environment = new Dictionary<string, string?> { ["NSCHEMA_DATABASE_CONNECTION_STRING"] = "Host=live" };

        // Act
        var definition = Assemble("DATABASE pg ( );", environment);

        // Assert
        definition.Database!.Value("connection_string").ShouldBe("Host=live");
    }

    [Fact]
    public void Override_AppliesToEverySetting_NotAKnownList()
    {
        // Arrange — nothing in Core knows what 'username' means; the rule is the name, not a per-plugin list.
        var environment = new Dictionary<string, string?>
        {
            ["NSCHEMA_DATABASE_USERNAME"] = "deploy",
            ["NSCHEMA_DATABASE_PASSWORD"] = "hunter2",
        };

        // Act
        var definition = Assemble("DATABASE pg ( );", environment);

        // Assert
        definition.Database!.Value("username").ShouldBe("deploy");
        definition.Database.Value("password").ShouldBe("hunter2");
    }

    [Fact]
    public void Override_IsScopedToItsStatementKeyword()
    {
        // Arrange — both statements take a connection string; the keyword in the name says which is meant.
        var environment = new Dictionary<string, string?>
        {
            ["NSCHEMA_DATABASE_CONNECTION_STRING"] = "Host=live",
            ["NSCHEMA_STATE_CONNECTION_STRING"] = "Host=state",
        };

        // Act
        var definition = Assemble("DATABASE pg ( );\nSTATE s3 ( );", environment);

        // Assert
        definition.Database!.Value("connection_string").ShouldBe("Host=live");
        definition.State!.Value("connection_string").ShouldBe("Host=state");
    }

    [Fact]
    public void UnrelatedVariables_AreIgnored()
    {
        // Arrange
        var environment = new Dictionary<string, string?>
        {
            ["PATH"] = "/usr/bin",
            ["NSCHEMA_ENVIRONMENT"] = "prod",
            ["NSCHEMA_STATE_BUCKET"] = "state-bucket",
        };

        // Act
        var definition = Assemble("DATABASE pg ( host = 'localhost' );", environment);

        // Assert — only the DATABASE statement's own prefix contributes.
        definition.Database!.Values.Keys.ShouldBe(["host"], ignoreOrder: true);
    }
}
