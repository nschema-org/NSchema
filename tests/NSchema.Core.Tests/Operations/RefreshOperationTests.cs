using NSchema.Model;
using NSchema.Model.Schemas;
using NSchema.Operations;
using NSchema.Operations.Workflow;
using NSchema.Plan.Domain;

namespace NSchema.Tests.Operations;

public sealed class RefreshOperationTests
{
    private readonly IMigrationWorkflow _workflow = Substitute.For<IMigrationWorkflow>();
    private readonly RefreshOperation _sut;

    public RefreshOperationTests() => _sut = new RefreshOperation(_workflow);

    [Fact]
    public async Task Execute_WhenStateCaptured_ReturnsTheSchemaAndSnapshotSize()
    {
        // Arrange
        var schema = new Database { Schemas = [new Schema { Name = "app" }] };
        _workflow.Refresh(Arg.Any<MigrationPlan?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(new StateCapture(schema, 2048));

        // Act
        var result = await _sut.Execute(new RefreshArguments(), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Database.ShouldBe(schema);
        result.Value!.SnapshotBytes.ShouldBe(2048);
    }

    [Fact]
    public async Task Execute_ForwardsForceToTheWorkflow()
    {
        // Arrange
        _workflow.Refresh(Arg.Any<MigrationPlan?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new StateCapture(new Database { Schemas = [] }, 64));

        // Act
        await _sut.Execute(new RefreshArguments { Force = true }, TestContext.Current.CancellationToken);

        // Assert
        await _workflow.Received(1).Refresh(null, force: true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_WhenTheCaptureRefusesUnreadableState_PropagatesTheFailure()
    {
        // Arrange — without force, an unreadable payload fails the refresh rather than being replaced.
        _workflow.Refresh(Arg.Any<MigrationPlan?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<StateCapture>(Diagnostic.Error("state", "unreadable")));

        // Act
        var result = await _sut.Execute(new RefreshArguments(), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().Message.ShouldBe("unreadable");
    }

    [Fact]
    public async Task Execute_ForwardsTheCapturesWarningsOntoTheResult()
    {
        // Arrange — the capture replaced state it couldn't read, resetting the run-once ledger.
        _workflow.Refresh(Arg.Any<MigrationPlan?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new StateCapture(new Database { Schemas = [] }, 64),
                Diagnostic.Warning("state", "the run-once script ledger was reset")));

        // Act
        var result = await _sut.Execute(new RefreshArguments(), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var warning = result.Diagnostics.ShouldHaveSingleItem();
        warning.Severity.ShouldBe(DiagnosticSeverity.Warning);
        warning.Message.ShouldContain("ledger was reset");
    }

    [Fact]
    public async Task Execute_WhenNoStoreConfigured_Fails()
    {
        // Arrange — refresh's whole purpose is to capture to a store, so a missing store is a failure (not a no-op).
        _workflow.Refresh(Arg.Any<MigrationPlan?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<StateCapture>(Diagnostic.Error("refresh", "Unable to refresh state without a configured state store.")));

        // Act
        var result = await _sut.Execute(new RefreshArguments(), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().Message.ShouldContain("without a configured state store");
    }
}
