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
        names.ShouldBe(["init", "validate", "plan", "apply", "refresh", "import", "destroy", "show", "drift", "force-unlock"], ignoreOrder: true);
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
        // The live database (PROVIDER block) and state store (BACKEND block) are defined in the project's .sql config
        // blocks — with the connection string overridable via NSCHEMA_POSTGRES_CONNECTION_STRING — so these are
        // rejected as unknown flags.
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
    public void Force_IsAcceptedByForceUnlock()
    {
        // Act — force-unlock skips its confirmation prompt with --force (Terraform's force-unlock -force).
        var result = _sut.Parse(["force-unlock", "--force"]);

        // Assert
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void AutoApprove_IsRejectedByForceUnlock()
    {
        // Act — force-unlock uses --force, not the apply/destroy --auto-approve flag.
        var result = _sut.Parse(["force-unlock", "--auto-approve"]);

        // Assert
        result.Errors.ShouldNotBeEmpty();
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
    public void Out_IsAcceptedByPlan()
    {
        // Act — plan --out saves the computed plan for later replay (Terraform's plan -out).
        var result = _sut.Parse(["plan", "--out", "plan.nschema"]);

        // Assert
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void Out_IsRejectedByApply()
    {
        // Act — saving a plan is a plan-only concern; apply consumes one with --plan-file.
        var result = _sut.Parse(["apply", "--out", "plan.nschema"]);

        // Assert
        result.Errors.ShouldNotBeEmpty();
    }

    [Fact]
    public void PlanFile_IsAcceptedByApply()
    {
        // Act — apply --plan-file replays a saved plan (Terraform's apply <planfile>).
        var result = _sut.Parse(["apply", "--plan-file", "plan.nschema"]);

        // Assert
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void PlanFile_IsRejectedByPlan()
    {
        // Act — plan computes and (optionally) saves a plan; it never consumes one.
        var result = _sut.Parse(["plan", "--plan-file", "plan.nschema"]);

        // Assert
        result.Errors.ShouldNotBeEmpty();
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
    [InlineData("force-unlock")]
    public void Directory_IsAcceptedAfterEveryCommand(string command)
    {
        // --directory is a recursive root option, so it can follow the subcommand on any command.
        var result = _sut.Parse([command, "--directory", "."]);

        // Assert
        result.Errors.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("validate")]
    [InlineData("plan")]
    [InlineData("apply")]
    [InlineData("refresh")]
    [InlineData("import")]
    [InlineData("destroy")]
    [InlineData("show")]
    [InlineData("drift")]
    [InlineData("force-unlock")]
    public void Environment_IsAcceptedByEveryEnvironmentAwareCommand(string command)
    {
        // --environment selects the per-environment overlay config; it's a recursive root option, so it follows any
        // command. (init is excluded — it scaffolds a project rather than acting on an environment.)
        var result = _sut.Parse([command, "--environment", "prod"]);

        // Assert
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void Validate_AcceptsDirectory()
    {
        // Act — validate reads the schema from the project's .sql files; it exposes no schema flags of its own.
        var result = _sut.Parse(["validate", "--directory", "."]);

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
