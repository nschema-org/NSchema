using NSchema.Hosting;
using NSchema.Migration;
using NSchema.Migration.Plan;
using NSchema.Policies;
using NSchema.Schema;
using NSubstitute.ExceptionExtensions;

namespace NSchema.Tests.Hosting;

public sealed class DefaultMigrationPipelineTests
{
    private readonly IMigrationPlanner _planner = Substitute.For<IMigrationPlanner>();
    private readonly IMigrationReporter _reporter = Substitute.For<IMigrationReporter>();
    private readonly IMigrationCompiler _compiler = Substitute.For<IMigrationCompiler>();
    private readonly ICompiledMigration _execution = Substitute.For<ICompiledMigration>();
    private readonly IStateCapturer _stateCapturer = Substitute.For<IStateCapturer>();

    private readonly DefaultMigrationPipeline _sut;

    public DefaultMigrationPipelineTests()
    {
        _planner
            .Plan(Arg.Any<CancellationToken>())
            .Returns(new MigrationPlan([], DatabaseSchema.Create([])));

        _execution.Preview.Returns([]);
        _compiler.Compile(Arg.Any<MigrationPlan>(), Arg.Any<CancellationToken>()).Returns(_execution);

        _sut = new DefaultMigrationPipeline(_planner, _reporter, _compiler, _stateCapturer);
    }

    [Fact]
    public async Task Plan_CompilesButDoesNotExecute()
    {
        // Arrange

        // Act
        await _sut.Plan();

        // Assert
        await _compiler.Received(1).Compile(Arg.Any<MigrationPlan>(), Arg.Any<CancellationToken>());
        await _execution.DidNotReceive().Execute(Arg.Any<CancellationToken>());
        await _stateCapturer.DidNotReceive().Capture(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Apply_CompilesExecutesAndCapturesState()
    {
        // Arrange
        var plan = new MigrationPlan([new CreateSchema("app")], DatabaseSchema.Create([]));
        _planner.Plan(Arg.Any<CancellationToken>()).Returns(plan);

        // Act
        await _sut.Apply();

        // Assert
        await _compiler.Received(1).Compile(plan, Arg.Any<CancellationToken>());
        await _execution.Received(1).Execute(Arg.Any<CancellationToken>());
        await _stateCapturer.Received(1).Capture(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Apply_DoesNotCaptureState_WhenExecutionFails()
    {
        // Arrange
        _execution.Execute(Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("boom"));

        // Act
        var act = () => _sut.Apply();

        // Assert
        await Should.ThrowAsync<InvalidOperationException>(act);
        await _stateCapturer.DidNotReceive().Capture(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Apply_EmptyPlan_StillExecutes()
    {
        // Arrange

        // Act
        await _sut.Apply();

        // Assert
        await _execution.Received(1).Execute(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Plan_PresentsPlanAndPreviewToReporter()
    {
        // Arrange
        var plan = new MigrationPlan([new CreateSchema("app")], DatabaseSchema.Create([]));
        _planner.Plan(Arg.Any<CancellationToken>()).Returns(plan);
        _execution.Preview.Returns(["CREATE SCHEMA app", "CREATE TABLE app.users (id int)"]);

        // Act
        await _sut.Plan();

        // Assert: the pipeline hands artifacts to the reporter; rendering is the reporter's concern.
        _reporter.Received(1).ReportPlan(plan);
        _reporter.Received(1).ReportPreview(Arg.Is<IReadOnlyList<string>>(p =>
            p.SequenceEqual(new[] { "CREATE SCHEMA app", "CREATE TABLE app.users (id int)" })));
    }

    [Fact]
    public async Task Apply_PolicyViolation_ReportsErrorsAndRethrows()
    {
        // Arrange
        var errors = new[] { new PolicyError("P1", "msg1"), new PolicyError("P2", "msg2") };
        _planner
            .Plan(Arg.Any<CancellationToken>())
            .Throws(new PolicyViolationException(errors));

        // Act
        var act = () => _sut.Apply();

        // Assert
        act.ShouldThrow<PolicyViolationException>();
        _reporter.Received().Error(Arg.Is<string>(s => s.Contains("msg1")));
        _reporter.Received().Error(Arg.Is<string>(s => s.Contains("msg2")));
        await _compiler.DidNotReceive().Compile(Arg.Any<MigrationPlan>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Apply_ExecutionThrows_ReportsErrorAndRethrows()
    {
        // Arrange
        _execution
            .Execute(Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("boom"));

        // Act
        var act = async () => await _sut.Apply();

        // Assert
        act.ShouldThrow<InvalidOperationException>();
        _reporter.Received().Error(Arg.Is<string>(s => s.Contains("boom")));
    }
}
