using System.CommandLine;

namespace NSchema.Tests.Commands;

public sealed class RootCommandTests
{
    private readonly RootCommand _sut = NSchema.Commands.RootCommand.Create();

    [Fact]
    public void RegistersTheExpectedCommands()
    {
        // Act
        var names = _sut.Subcommands.Select(command => command.Name);

        // Assert
        names.ShouldBe(["init", "validate", "plan", "apply", "refresh", "import", "destroy"], ignoreOrder: true);
    }

    [Theory]
    [InlineData("plan")]
    [InlineData("apply")]
    [InlineData("refresh")]
    [InlineData("destroy")]
    public void ProviderAndStateOptions_AreNotCliFlags(string command)
    {
        // The live database (provider.postgres) and state store are defined in nschema.json — with the connection
        // string supplied via the NSCHEMA_POSTGRES_CONNECTION_STRING env var — so these are rejected as unknown flags.
        var result = _sut.Parse([command, "--provider", "postgres", "--connection-string", "x", "--state-file", "s"]);

        // Assert
        result.Errors.ShouldNotBeEmpty();
    }

    [Fact]
    public void AutoApprove_IsAcceptedByApply()
    {
        // Act
        var result = _sut.Parse(["apply", "--auto-approve"]);

        // Assert
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void AutoApprove_IsAcceptedByDestroy()
    {
        // Act
        var result = _sut.Parse(["destroy", "--auto-approve"]);

        // Assert
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void AutoApprove_IsRejectedByPlan()
    {
        // Act
        var result = _sut.Parse(["plan", "--auto-approve"]);

        // Assert
        result.Errors.ShouldNotBeEmpty();
    }

    [Theory]
    [InlineData("plan")]
    [InlineData("apply")]
    public void DesiredAndMigrationOptions_AreAcceptedByPlanAndApply(string command)
    {
        // Act — schema format/pattern are config-only; the schema directory and migration knobs stay flags.
        var result = _sut.Parse([command, "--schema-dir", "d", "--scope", "public", "--destructive-actions", "Warn"]);

        // Assert
        result.Errors.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("--scope", "public")]
    [InlineData("--destructive-actions", "Warn")]
    [InlineData("--format", "json")]
    [InlineData("--schema-dir", "d")]
    public void RefreshRejects_DesiredAndMigrationOptions(string option, string value)
    {
        // Act
        var result = _sut.Parse(["refresh", option, value]);

        // Assert
        result.Errors.ShouldNotBeEmpty();
    }

    [Fact]
    public void Validate_AcceptsSchemaOptions()
    {
        // Act — schema format/pattern are config-only; validate takes the directory and --config.
        var result = _sut.Parse(["validate", "--schema-dir", "d", "--config", "c"]);

        // Assert
        result.Errors.ShouldBeEmpty();
        result.CommandResult.Command.Name.ShouldBe("validate");
    }

    [Theory]
    [InlineData("--scope", "public")]
    [InlineData("--destructive-actions", "Warn")]
    public void ValidateRejects_MigrationOptions(string option, string value)
    {
        // Act
        var result = _sut.Parse(["validate", option, value]);

        // Assert
        result.Errors.ShouldNotBeEmpty();
    }
}
