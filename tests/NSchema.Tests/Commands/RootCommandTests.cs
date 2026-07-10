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
        names.ShouldBe(["init", "scaffold", "validate", "fmt", "plan", "apply", "refresh", "import", "destroy", "state", "script", "db", "drift", "doctor", "lock", "plugin", "completion"], ignoreOrder: true);
    }

    [Fact]
    public void PluginGroup_RegistersListShowAndCache()
    {
        // Act — the plugin noun groups project-plugin inspection (list/show) and the cache sub-noun.
        var pluginCommand = _sut.Subcommands.Single(command => command.Name == "plugin");
        var names = pluginCommand.Subcommands.Select(command => command.Name);

        // Assert
        names.ShouldBe(["list", "show", "cache"], ignoreOrder: true);
    }

    [Fact]
    public void PluginCacheGroup_RegistersListRemoveAndClear()
    {
        // Act — the cache sub-noun groups the profile-level, project-independent cache operations.
        var cacheCommand = _sut.Subcommands.Single(command => command.Name == "plugin")
            .Subcommands.Single(command => command.Name == "cache");
        var names = cacheCommand.Subcommands.Select(command => command.Name);

        // Assert
        names.ShouldBe(["list", "remove", "clear"], ignoreOrder: true);
    }

    [Fact]
    public void PluginCacheClear_TakesNoArguments()
        // clear wipes the whole cache; the destructive "all" is its own named verb, not a bare `remove`.
        => _sut.Parse(["plugin", "cache", "clear"]).Errors.ShouldBeEmpty();

    [Fact]
    public void PluginShow_RequiresALabel()
        // The plugin to show is mandatory; bare `plugin show` is a usage error.
        => _sut.Parse(["plugin", "show"]).Errors.ShouldNotBeEmpty();

    [Fact]
    public void PluginShow_AcceptsAPositionalLabel()
        => _sut.Parse(["plugin", "show", "postgres"]).Errors.ShouldBeEmpty();

    [Fact]
    public void PluginCacheRemove_RequiresAPackage()
        // The package to remove is mandatory; bare `plugin cache remove` is a usage error.
        => _sut.Parse(["plugin", "cache", "remove"]).Errors.ShouldNotBeEmpty();

    [Fact]
    public void PluginCacheRemove_AcceptsPackageWithOptionalVersion()
    {
        // Both forms parse: a bare package (all versions) and a package + version.
        _sut.Parse(["plugin", "cache", "remove", "NSchema.Postgres"]).Errors.ShouldBeEmpty();
        _sut.Parse(["plugin", "cache", "remove", "NSchema.Postgres", "4.0.0"]).Errors.ShouldBeEmpty();
    }

    [Fact]
    public void LockGroup_RegistersStatusAcquireAndRelease()
    {
        // Act — the lock noun groups the thin IStateLock front-ends.
        var lockCommand = _sut.Subcommands.Single(command => command.Name == "lock");
        var names = lockCommand.Subcommands.Select(command => command.Name);

        // Assert
        names.ShouldBe(["status", "acquire", "release"], ignoreOrder: true);
    }

    [Fact]
    public void StateGroup_RegistersShowPullAndPush()
    {
        // Act — the state noun groups recorded-state inspection and surgery.
        var stateCommand = _sut.Subcommands.Single(command => command.Name == "state");
        var names = stateCommand.Subcommands.Select(command => command.Name);

        // Assert
        names.ShouldBe(["show", "pull", "push"], ignoreOrder: true);
    }

    [Fact]
    public void ScriptGroup_RegistersListTaintAndUntaint()
    {
        // Act — the script noun manages the execution ledger recorded in the state.
        var scriptCommand = _sut.Subcommands.Single(command => command.Name == "script");
        var names = scriptCommand.Subcommands.Select(command => command.Name);

        // Assert
        names.ShouldBe(["list", "hash", "taint", "untaint"], ignoreOrder: true);
    }

    [Fact]
    public void DbGroup_RegistersShow()
    {
        // Act — the db noun groups live-database inspection (the online counterpart to `state show`).
        var dbCommand = _sut.Subcommands.Single(command => command.Name == "db");
        var names = dbCommand.Subcommands.Select(command => command.Name);

        // Assert
        names.ShouldBe(["show"], ignoreOrder: true);
    }

    [Fact]
    public void PlanGroup_RegistersShowSubcommand()
        // plan keeps its compute action (git-stash style) and gains `plan show <file>` for saved plans.
        => _sut.Subcommands.Single(command => command.Name == "plan")
            .Subcommands.Select(command => command.Name).ShouldContain("show");

    [Theory]
    [InlineData("plan")]
    [InlineData("apply")]
    [InlineData("refresh")]
    [InlineData("destroy")]
    [InlineData("state show")]
    [InlineData("db show")]
    [InlineData("drift")]
    public void ProviderAndStateOptions_AreNotCliFlags(string command)
    {
        // The live database (PROVIDER block) and state store (BACKEND block) are defined in the project's .sql config
        // blocks — with the connection string overridable via NSCHEMA_POSTGRES_CONNECTION_STRING — so these are
        // rejected as unknown flags.
        var result = _sut.Parse([.. command.Split(' '), "--provider", "postgres", "--connection-string", "x", "--state-file", "s"]);

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
    public void AutoApprove_IsAcceptedByLockRelease()
    {
        // Act — lock release skips its confirmation prompt with --auto-approve, consistent with apply/destroy.
        var result = _sut.Parse(["lock", "release", "--auto-approve"]);

        // Assert
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void LockRelease_AcceptsAPositionalLockId()
        // lock release <lock-id> releases a specific lock (compare-and-swap), Terraform's force-unlock LOCK_ID.
        => _sut.Parse(["lock", "release", "9f8e7d6c"]).Errors.ShouldBeEmpty();

    [Fact]
    public void LockRelease_LockIdArgumentIsOptional()
        // Bare `lock release` still releases whatever lock is held, so the positional must be optional.
        => _sut.Parse(["lock", "release"]).Errors.ShouldBeEmpty();

    [Fact]
    public void LockAcquire_AcceptsReasonAndTtl()
        // lock acquire holds the lock for out-of-band work; --reason annotates it and --ttl gives it an expiry.
        => _sut.Parse(["lock", "acquire", "--reason", "manual db surgery", "--ttl", "30m"]).Errors.ShouldBeEmpty();

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
        var result = _sut.Parse([command, "--scope", "public", "--destructive-actions", "Warn", "--data-hazards", "Error"]);

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

    [Fact]
    public void NoInit_IsAcceptedByOperations()
        // --no-init skips the implicit plugin restore (cache-only); it's a recursive root option.
        => _sut.Parse(["plan", "--no-init"]).Errors.ShouldBeEmpty();

    [Theory]
    [InlineData("--scope", "public")]
    [InlineData("--destructive-actions", "Warn")]
    [InlineData("--data-hazards", "Warn")]
    public void RefreshRejects_MigrationOptions(string option, string value)
    {
        // Act
        var result = _sut.Parse(["refresh", option, value]);

        // Assert
        result.Errors.ShouldNotBeEmpty();
    }

    [Theory]
    [InlineData("init")]
    [InlineData("scaffold")]
    [InlineData("validate")]
    [InlineData("plan")]
    [InlineData("apply")]
    [InlineData("refresh")]
    [InlineData("import")]
    [InlineData("destroy")]
    [InlineData("state show")]
    [InlineData("drift")]
    [InlineData("doctor")]
    [InlineData("lock status")]
    [InlineData("lock acquire")]
    [InlineData("lock release")]
    public void Directory_IsAcceptedAfterEveryCommand(string command)
    {
        // --directory is a recursive root option, so it can follow the subcommand on any command.
        var result = _sut.Parse([.. command.Split(' '), "--directory", "."]);

        // Assert
        result.Errors.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("init")]
    [InlineData("validate")]
    [InlineData("plan")]
    [InlineData("apply")]
    [InlineData("refresh")]
    [InlineData("import")]
    [InlineData("destroy")]
    [InlineData("state show")]
    [InlineData("drift")]
    [InlineData("doctor")]
    [InlineData("lock status")]
    [InlineData("lock acquire")]
    [InlineData("lock release")]
    public void Environment_IsAcceptedByEveryEnvironmentAwareCommand(string command)
    {
        // --environment selects the per-environment overlay config; it's a recursive root option, so it follows any
        // command. (scaffold is excluded — it scaffolds a project rather than acting on an environment.)
        var result = _sut.Parse([.. command.Split(' '), "--environment", "prod"]);

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
    [InlineData("--data-hazards", "Warn")]
    public void ValidateRejects_MigrationOptions(string option, string value)
    {
        // Act
        var result = _sut.Parse(["validate", option, value]);

        // Assert
        result.Errors.ShouldNotBeEmpty();
    }

    [Theory]
    [InlineData("state show")]
    [InlineData("drift")]
    public void Scope_IsAcceptedByStateShowAndDrift(string command)
    {
        // Act — state show and drift filter the recorded state by namespace, but expose no destructive-action knob.
        var result = _sut.Parse([.. command.Split(' '), "--scope", "public"]);

        // Assert
        result.Errors.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("state show", "--destructive-actions")]
    [InlineData("state show", "--data-hazards")]
    [InlineData("drift", "--destructive-actions")]
    [InlineData("drift", "--data-hazards")]
    public void StateShowAndDriftReject_PolicyOptions(string command, string option)
    {
        // Act — neither command produces a migration, so the diff policies are meaningless.
        var result = _sut.Parse([.. command.Split(' '), option, "Warn"]);

        // Assert
        result.Errors.ShouldNotBeEmpty();
    }

    [Fact]
    public void StateShow_AcceptsAPositionalStateFile()
        // state show <file> renders a state file directly instead of the configured store.
        => _sut.Parse(["state", "show", "state.json"]).Errors.ShouldBeEmpty();

    [Fact]
    public void StateShow_FileArgumentIsOptional()
        // Bare `state show` reads the configured store, so the positional must be optional.
        => _sut.Parse(["state", "show"]).Errors.ShouldBeEmpty();

    [Fact]
    public void PlanShow_AcceptsAPositionalPlanFile()
        // plan show <file> renders a saved plan's diff/plan/SQL (was the old top-level `show <planfile>`).
        => _sut.Parse(["plan", "show", "plan.nschema"]).Errors.ShouldBeEmpty();

    [Fact]
    public void PlanShow_RequiresAFile()
        // The saved plan to render is mandatory; bare `plan show` is a usage error.
        => _sut.Parse(["plan", "show"]).Errors.ShouldNotBeEmpty();

    [Theory]
    [InlineData("--scope", "public")]
    [InlineData("--destructive-actions", "Warn")]
    [InlineData("--data-hazards", "Warn")]
    public void DoctorRejects_MigrationOptions(string option, string value)
    {
        // Act — doctor is a bare health check; it produces no migration, so it exposes no scope/policy knobs.
        var result = _sut.Parse(["doctor", option, value]);

        // Assert
        result.Errors.ShouldNotBeEmpty();
    }

    [Fact]
    public void DetailedExitCode_IsAcceptedByLockStatus()
        // lock status --detailed-exitcode opts into exit 2 when the state is locked, for CI gating.
        => _sut.Parse(["lock", "status", "--detailed-exitcode"]).Errors.ShouldBeEmpty();

    [Theory]
    [InlineData("--scope", "public")]
    [InlineData("--destructive-actions", "Warn")]
    [InlineData("--data-hazards", "Warn")]
    public void LockStatusRejects_MigrationOptions(string option, string value)
    {
        // Act — lock status only reads the lock; it produces no migration, so it exposes no scope/policy knobs.
        var result = _sut.Parse(["lock", "status", option, value]);

        // Assert
        result.Errors.ShouldNotBeEmpty();
    }

    [Theory]
    [InlineData("apply")]
    [InlineData("refresh")]
    [InlineData("destroy")]
    public void NoLock_IsAcceptedByMutatingCommands(string command)
        // --no-lock runs the operation without taking the state lock (e.g. under a manually-held lock).
        => _sut.Parse([command, "--no-lock"]).Errors.ShouldBeEmpty();

    [Theory]
    [InlineData("plan")]
    [InlineData("drift")]
    public void NoLock_IsRejectedByReadOnlyCommands(string command)
        // --no-lock is meaningless for commands that never take the lock.
        => _sut.Parse([command, "--no-lock"]).Errors.ShouldNotBeEmpty();

    [Fact]
    public void Fmt_AcceptsAPositionalPathAndCheck()
        // fmt <path> --check formats (or checks) the .sql files under a file/dir (Terraform's fmt -check).
        => _sut.Parse(["fmt", "schema.sql", "--check"]).Errors.ShouldBeEmpty();

    [Fact]
    public void Fmt_PathArgumentIsOptional()
        // Bare `fmt` formats the current directory, so the positional must be optional.
        => _sut.Parse(["fmt"]).Errors.ShouldBeEmpty();
}
