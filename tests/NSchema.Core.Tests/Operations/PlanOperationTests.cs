using NSchema.Diff.Domain;
using NSchema.Diff.Domain.Schemas;
using NSchema.Model;
using NSchema.Operations;
using NSchema.Operations.Workflow;
using NSchema.Plan.Domain;
using NSchema.Plan.PlanFile;

namespace NSchema.Tests.Operations;

public sealed class PlanOperationTests
{
    private readonly IMigrationWorkflow _workflow = Substitute.For<IMigrationWorkflow>();
    private readonly IPlanFileManager _planFile = Substitute.For<IPlanFileManager>();

    private readonly MigrationPlan _plan = new(
        new DatabaseDiff([SchemaDiff.Added("app")]),
        [new SqlStatement("CREATE SCHEMA app")]);

    private readonly PlanOperation _sut;

    public PlanOperationTests()
    {
        _workflow.ComputePlan(Arg.Any<PlanTarget>(), Arg.Any<PlanningScope>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(_plan));

        _sut = new PlanOperation(_workflow, _planFile);
    }

    private static PlanArguments Args(PlanTarget target = PlanTarget.Project, PlanningScope? scope = null, string? outFile = null) =>
        new() { Target = target, Scope = scope ?? PlanningScope.All, OutFile = outFile };

    [Theory]
    [InlineData(PlanTarget.Project)]
    [InlineData(PlanTarget.Empty)]
    public async Task Execute_ForwardsTargetToComputePlan(PlanTarget target)
    {
        // Act — a teardown is not a separate operation shape; it is the same plan path with an empty target.
        await _sut.Execute(Args(target), TestContext.Current.CancellationToken);

        // Assert
        await _workflow.Received(1).ComputePlan(target, Arg.Any<PlanningScope>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_ForwardsScopeToComputePlan()
    {
        // Act
        await _sut.Execute(Args(scope: PlanningScope.To(DatabaseAddress.Schema("app"), DatabaseAddress.Schema("legacy"))), TestContext.Current.CancellationToken);

        // Assert
        await _workflow.Received(1).ComputePlan(
            Arg.Any<PlanTarget>(),
            Arg.Is<PlanningScope>(s => !s!.IsUnscoped && s.Contains("app") && s.Contains("legacy")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_Teardown_ForwardsScopeToComputePlan()
    {
        // Act — scoping is no longer special-cased: a teardown narrows like any other plan.
        await _sut.Execute(Args(PlanTarget.Empty, scope: PlanningScope.To(DatabaseAddress.Schema("app"))), TestContext.Current.CancellationToken);

        // Assert
        await _workflow.Received(1).ComputePlan(
            PlanTarget.Empty,
            Arg.Is<PlanningScope>(s => !s!.IsUnscoped && s.Contains("app")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_OnSuccess_ProducesThePlan()
    {
        // Act
        var result = await _sut.Execute(Args(), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Plan.ShouldBe(_plan);
        result.Value!.HasChanges.ShouldBeTrue();
    }

    [Fact]
    public async Task Execute_OnSuccess_WithOutFile_WritesTheEnvelope()
    {
        // Act
        await _sut.Execute(Args(outFile: "plan.nschema"), TestContext.Current.CancellationToken);

        // Assert
        await _planFile.Received(1).Write(
            "plan.nschema",
            Arg.Is<PlanFileEnvelope>(e => e!.Plan == _plan),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_OnPolicyFailure_StillCarriesTheCompletePlan()
    {
        // Arrange — a policy blocks the plan; the failure still carries the full artifact so the offending
        // change (and its SQL) stays visible.
        _workflow.ComputePlan(Arg.Any<PlanTarget>(), Arg.Any<PlanningScope>(), Arg.Any<CancellationToken>())
            .Returns(Result.From(_plan, [Diagnostic.Error("destructive", "drops-table", "drops table")]));

        // Act
        var result = await _sut.Execute(Args(), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Value!.Plan.ShouldBe(_plan);
        result.Errors.ShouldContain(d => d.Message == "drops table");
    }

    [Fact]
    public async Task Execute_OnPolicyFailure_WithOutFile_StillWritesThePlan()
    {
        // Arrange — the file is a review artifact, not a bypass: apply enforces the policies again against the
        // carried diff, so writing a blocked plan is safe and the failing result reports the block.
        _workflow.ComputePlan(Arg.Any<PlanTarget>(), Arg.Any<PlanningScope>(), Arg.Any<CancellationToken>())
            .Returns(Result.From(_plan, [Diagnostic.Error("destructive", "drops-table", "drops table")]));

        // Act
        var result = await _sut.Execute(Args(outFile: "plan.nschema"), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        await _planFile.Received(1).Write("plan.nschema", Arg.Is<PlanFileEnvelope>(e => e!.Plan == _plan), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_WhenPlanningProducesNoValue_CarriesNoPlan()
    {
        // Arrange — planning could not run at all (e.g. no provider registered).
        _workflow.ComputePlan(Arg.Any<PlanTarget>(), Arg.Any<PlanningScope>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<MigrationPlan>(Diagnostic.Error("plan", "no-provider", "no provider")));

        // Act
        var result = await _sut.Execute(Args(), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Value!.Plan.ShouldBeNull();
        result.Value!.HasChanges.ShouldBeFalse();
    }
}
