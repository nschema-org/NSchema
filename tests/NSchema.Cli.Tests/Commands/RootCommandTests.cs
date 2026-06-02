using System.CommandLine;
using CliCommands = NSchema.Cli.Commands;

namespace NSchema.Cli.Tests.Commands;

public sealed class RootCommandTests
{
    private readonly RootCommand _sut = CliCommands.RootCommand.Create();

    [Fact]
    public void HasTheNschemaCommandName()
    {
        // Guards the reflection in RootCommand.Create that overrides the executable-derived name.
        _sut.Name.ShouldBe("nschema");
    }

    [Fact]
    public void RegistersTheExpectedCommands()
    {
        // Act
        var names = _sut.Subcommands.Select(command => command.Name);

        // Assert
        names.ShouldBe(["init", "plan", "apply", "refresh"], ignoreOrder: true);
    }

    [Theory]
    [InlineData("plan")]
    [InlineData("apply")]
    [InlineData("refresh")]
    public void GlobalOptions_AreAvailableToEveryCommand(string command)
    {
        // Act
        var result = _sut.Parse([command, "--provider", "postgres", "--connection-string", "x", "--state-file", "s"]);

        // Assert
        result.Errors.ShouldBeEmpty();
        result.CommandResult.Command.Name.ShouldBe(command);
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
        // Act
        var result = _sut.Parse(
            [command, "--format", "json", "--schema-dir", "d", "--schema-glob", "g", "--scope", "public", "--destructive-actions", "Warn"]);

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
}
