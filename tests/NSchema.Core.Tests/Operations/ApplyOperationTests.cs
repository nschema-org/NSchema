using NSchema.Apply;
using NSchema.Diff.Domain;
using NSchema.Model;
using NSchema.Model.Scripts;
using NSchema.Operations;
using NSchema.Operations.Progress;
using NSchema.Operations.Workflow;
using NSchema.Plan.Domain;
using NSchema.Plan.Policies;
using NSchema.State;
using NSubstitute.ExceptionExtensions;

namespace NSchema.Tests.Operations;

public sealed class ApplyOperationTests
{
    private readonly IMigrationWorkflow _workflow = Substitute.For<IMigrationWorkflow>();
    private readonly IProgress<OperationProgress> _progress = Substitute.For<IProgress<OperationProgress>>();
    private readonly ISqlExecutor _executor = Substitute.For<ISqlExecutor>();
    private readonly List<IPlanPolicy> _planPolicies = [];
    private readonly IDatabaseStateManager _stateManager = Substitute.For<IDatabaseStateManager>();

    private readonly MigrationPlan _plan = new(new DatabaseDiff([]), [new SqlStatement("CREATE SCHEMA app")]);
    private static readonly MigrationPlan _emptyPlan = new(new DatabaseDiff([]), []);

    private ApplyOperation BuildSut(ISqlExecutor? executor) => new(_workflow, _progress, _planPolicies, _stateManager, executor);

    private readonly ApplyOperation _sut;

    public ApplyOperationTests()
    {
        _stateManager.IsConfigured.Returns(true);
        _workflow.Refresh(Arg.Any<MigrationPlan?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new StateCapture(new Database { Schemas = [] }, 0)));
        _sut = BuildSut(_executor);
    }

    [Fact]
    public async Task Execute_WithoutAStateStore_FailsBeforeExecutingAnything()
    {
        // Arrange — applying records the run (capture + run-once ledger), so an unrecordable run is refused.
        _stateManager.IsConfigured.Returns(false);

        // Act
        var result = await _sut.Execute(Args(_plan), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().ShouldBe(ApplyDiagnostics.StoreRequired);
        await _executor.DidNotReceive().Execute(Arg.Any<IReadOnlyList<SqlStatement>>(), Arg.Any<CancellationToken>());
    }

    private static ApplyArguments Args(MigrationPlan plan) => new() { Plan = plan };

    [Fact]
    public async Task Execute_RunsSqlThenRefreshesState()
    {
        // Act
        var result = await _sut.Execute(Args(_plan), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value!.AppliedPlan.ShouldBe(_plan);
        result.Value!.ChangesApplied.ShouldBeTrue();
        result.Value!.StatementsExecuted.ShouldBe(1);
        await _executor.Received(1).Execute(_plan.Statements, Arg.Any<CancellationToken>());
        await _workflow.Received(1).Refresh(Arg.Any<MigrationPlan?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_EmptyPlan_SkipsExecutionButStillRefreshes()
    {
        // Act
        var result = await _sut.Execute(Args(_emptyPlan), TestContext.Current.CancellationToken);

        // Assert
        // An empty plan applied nothing, but a first run against an already-matching target still initialises the store.
        result.Value!.ChangesApplied.ShouldBeFalse();
        result.Value!.StatementsExecuted.ShouldBe(0);
        await _executor.DidNotReceive().Execute(Arg.Any<IReadOnlyList<SqlStatement>>(), Arg.Any<CancellationToken>());
        await _workflow.Received(1).Refresh(Arg.Any<MigrationPlan?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_Success_PassesTheAppliedPlanToTheCapture()
    {
        // Arrange — the plan carries its scripts whole; the capture derives the run-once ledger records from it.
        var plan = _plan with { Diff = new DatabaseDiff([]) { DeploymentScripts = [new DeploymentScript("seed", "SELECT 1", null, DeploymentPhase.Post)] } };

        // Act
        await _sut.Execute(Args(plan), TestContext.Current.CancellationToken);

        // Assert — the successful capture receives exactly the plan that ran.
        await _workflow.Received(1).Refresh(plan, Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_Failure_DoesNotPassTheAppliedPlan()
    {
        // Arrange — whether the plan's run-once scripts ran before the failure is unknowable, so nothing is recorded.
        _executor.Execute(Arg.Any<IReadOnlyList<SqlStatement>>(), Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("boom"));
        var plan = _plan with { Diff = new DatabaseDiff([]) { DeploymentScripts = [new DeploymentScript("seed", "SELECT 1", null, DeploymentPhase.Post)] } };

        // Act
        var result = await _sut.Execute(Args(plan), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        await _workflow.Received(1).Refresh((MigrationPlan?)null, Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_WhenExecutionFails_StillRefreshesAndFails()
    {
        // Arrange
        _executor.Execute(Arg.Any<IReadOnlyList<SqlStatement>>(), Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("boom"));

        // Act — execution may fail partway (e.g. an un-transacted plan), so state is still captured.
        var result = await _sut.Execute(Args(_plan), TestContext.Current.CancellationToken);

        // Assert — a database that refused the migration is an expected outcome, reported rather than thrown.
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(d => d.Message.Contains("boom"));
        await _workflow.Received(1).Refresh(Arg.Any<MigrationPlan?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_NonEmptyPlanButNoExecutor_Fails()
    {
        // Arrange
        var sut = BuildSut(executor: null);

        // Act
        var result = await sut.Execute(Args(_plan), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().ShouldBe(ApplyDiagnostics.MissingExecutor);
    }

    [Fact]
    public async Task Execute_WhenStateRefreshFailsAfterSuccessfulApply_Surfaces()
    {
        // The migration applies cleanly, but capturing the new state fails — recording state is part of the apply, so
        // this is a real error (the database changed but the recorded state is now stale), not something to swallow.
        _workflow.Refresh(Arg.Any<MigrationPlan?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("state store unreachable"));

        var ex = await Should.ThrowAsync<InvalidOperationException>(() => _sut.Execute(Args(_plan), TestContext.Current.CancellationToken));

        ex.Message.ShouldBe("state store unreachable");
        await _executor.Received(1).Execute(_plan.Statements, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_WhenBothExecutionAndStateRefreshFail_SurfacesTheExecutionError()
    {
        // A refresh failure during the best-effort capture must not mask the migration failure, which is the error the
        // operator needs to see.
        _executor.Execute(Arg.Any<IReadOnlyList<SqlStatement>>(), Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("migration failed"));
        _workflow.Refresh(Arg.Any<MigrationPlan?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("state store unreachable"));

        var result = await _sut.Execute(Args(_plan), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(d => d.Message.Contains("migration failed"));
        result.Errors.ShouldNotContain(d => d.Message.Contains("state store unreachable"));
        await _workflow.Received(1).Refresh(Arg.Any<MigrationPlan?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_PolicyBlockedPlan_IsNotApplied()
    {
        // Arrange — policies are enforced at the execution point against the exact plan being applied, so a saved plan
        // (older config, other tooling, hand edits) cannot slip past them.
        var policy = Substitute.For<IPlanPolicy>();
        policy.Validate(_plan).Returns([Diagnostic.Error("destructive", "drops-table", "drops table")]);
        _planPolicies.Add(policy);

        // Act
        var result = await _sut.Execute(Args(_plan), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(d => d.Message == "drops table");
        result.Errors.ShouldContain(d => d.Message.Contains("blocked by policy"));
        await _executor.DidNotReceive().Execute(Arg.Any<IReadOnlyList<SqlStatement>>(), Arg.Any<CancellationToken>());
        await _workflow.DidNotReceive().Refresh(Arg.Any<MigrationPlan?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_PolicyBlockedPlan_WithForce_AppliesWithTheErrorsDemotedToWarnings()
    {
        // Arrange — force is an informed override: the findings stay visible, demoted so they don't fail the run.
        var policy = Substitute.For<IPlanPolicy>();
        policy.Validate(_plan).Returns([Diagnostic.Error("destructive", "drops-table", "drops table")]);
        _planPolicies.Add(policy);

        // Act
        var result = await _sut.Execute(new ApplyArguments { Plan = _plan, Force = true }, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Diagnostics.ShouldContain(d => d.Message == "drops table" && d.Severity == DiagnosticSeverity.Warning);
        await _executor.Received(1).Execute(_plan.Statements, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_PolicyWarnings_RideTheResultAndDoNotBlock()
    {
        // Arrange
        var policy = Substitute.For<IPlanPolicy>();
        policy.Validate(_plan).Returns([Diagnostic.Warning("data-hazards", "risky-add", "risky add")]);
        _planPolicies.Add(policy);

        // Act
        var result = await _sut.Execute(Args(_plan), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Diagnostics.ShouldContain(d => d.Message == "risky add");
        await _executor.Received(1).Execute(_plan.Statements, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_EmptyPlan_WhenStateRefreshFails_Surfaces()
    {
        // Even with nothing to execute, the state capture is part of the apply; its failure is surfaced.
        _workflow.Refresh(Arg.Any<MigrationPlan?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("state store unreachable"));

        var ex = await Should.ThrowAsync<InvalidOperationException>(() => _sut.Execute(Args(_emptyPlan), TestContext.Current.CancellationToken));

        ex.Message.ShouldBe("state store unreachable");
    }
}
