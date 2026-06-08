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
        names.ShouldBe(["init", "validate", "plan", "apply", "refresh", "import", "destroy", "show", "drift"], ignoreOrder: true);
    }

    [Theory]
    [InlineData("plan")]
    [InlineData("apply")]
    [InlineData("refresh")]
    [InlineData("destroy")]
    [InlineData("show")]
    [InlineData("drift")]
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
    public void MigrationOptions_AreAcceptedByPlanAndApply(string command)
    {
        // Act — the schema (dir/format/pattern) is config-only now; only the migration knobs are flags.
        var result = _sut.Parse([command, "--scope", "public", "--destructive-actions", "Warn"]);

        // Assert
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void Destroy_IsAcceptedByPlan()
    {
        // Act — plan --destroy previews a teardown, Terraform-style.
        var result = _sut.Parse(["plan", "--destroy"]);

        // Assert
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void Destroy_IsRejectedByApply()
    {
        // Act — --destroy is a plan-only preview flag; tearing down for real is the dedicated destroy command.
        var result = _sut.Parse(["apply", "--destroy"]);

        // Assert
        result.Errors.ShouldNotBeEmpty();
    }

    [Theory]
    [InlineData("--scope", "public")]
    [InlineData("--destructive-actions", "Warn")]
    public void RefreshRejects_MigrationOptions(string option, string value)
    {
        // Act
        var result = _sut.Parse(["refresh", option, value]);

        // Assert
        result.Errors.ShouldNotBeEmpty();
    }

    [Theory]
    [InlineData("init")]
    [InlineData("validate")]
    [InlineData("plan")]
    [InlineData("apply")]
    [InlineData("refresh")]
    [InlineData("import")]
    [InlineData("destroy")]
    [InlineData("show")]
    [InlineData("drift")]
    public void Directory_IsAcceptedAfterEveryCommand(string command)
    {
        // --directory is a recursive root option, so it can follow the subcommand on any command.
        var result = _sut.Parse([command, "--directory", "."]);

        // Assert
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void Validate_AcceptsConfigAndDirectory()
    {
        // Act — validate reads the schema from config; it exposes no schema flags of its own.
        var result = _sut.Parse(["validate", "--config", "c", "--directory", "."]);

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

    [Theory]
    [InlineData("show")]
    [InlineData("drift")]
    public void Scope_IsAcceptedByShowAndDrift(string command)
    {
        // Act — show and drift filter the recorded state by namespace, but expose no destructive-action knob.
        var result = _sut.Parse([command, "--scope", "public"]);

        // Assert
        result.Errors.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("show")]
    [InlineData("drift")]
    public void ShowAndDriftReject_DestructiveActions(string command)
    {
        // Act — neither command produces a migration, so the destructive-action policy is meaningless.
        var result = _sut.Parse([command, "--destructive-actions", "Warn"]);

        // Assert
        result.Errors.ShouldNotBeEmpty();
    }
}
