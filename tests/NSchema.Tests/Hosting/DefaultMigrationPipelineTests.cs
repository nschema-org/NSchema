using Microsoft.Extensions.Options;
using NSchema.Hosting;
using NSchema.Migration;
using NSchema.Migration.Plan;
using NSchema.Policies;
using NSchema.Schema;
using NSubstitute.ExceptionExtensions;

namespace NSchema.Tests.Hosting;

public sealed class DefaultMigrationPipelineTests
{
    private readonly IOptions<MigrationOptions> _options = Options.Create(new MigrationOptions());
    private readonly IMigrationPlanner _planner = Substitute.For<IMigrationPlanner>();
    private readonly IMigrationReporter _reporter = Substitute.For<IMigrationReporter>();
    private readonly IMigrationPlanRenderer _renderer = Substitute.For<IMigrationPlanRenderer>();
    private readonly IMigrationExecutor _executor = Substitute.For<IMigrationExecutor>();

    private readonly DefaultMigrationPipeline _sut;

    public DefaultMigrationPipelineTests()
    {
        _planner
            .Plan(Arg.Any<CancellationToken>())
            .Returns(new MigrationPlan([], DatabaseSchema.Create([])));

        _sut = new DefaultMigrationPipeline(_options, _planner, _renderer, _reporter, _executor);
    }

    [Fact]
    public async Task Run_PlanOperation_InvokesExecutorWithDryRunFlag()
    {
        // Arrange
        _options.Value.Operation = MigrationOperation.Plan;

        // Act
        await _sut.Run();

        // Assert
        await _executor.Received(1).Apply(Arg.Any<MigrationPlan>(), true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Run_ApplyOperation_PassesPlanToExecutor()
    {
        // Arrange
        var plan = new MigrationPlan([new CreateSchema("app")], DatabaseSchema.Create([]));
        _planner.Plan(Arg.Any<CancellationToken>()).Returns(plan);

        // Act
        await _sut.Run();

        // Assert
        await _executor.Received(1).Apply(plan, false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Run_EmptyPlan_StillPassesPlanToExecutor()
    {
        // Arrange

        // Act
        await _sut.Run();

        // Assert
        await _executor.Received(1).Apply(Arg.Any<MigrationPlan>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Run_PolicyViolation_ReportsErrorsAndRethrows()
    {
        // Arrange
        var errors = new[] { new PolicyError("P1", "msg1"), new PolicyError("P2", "msg2") };
        _planner
            .Plan(Arg.Any<CancellationToken>())
            .Throws(new PolicyViolationException(errors));

        // Act
        var act = () => _sut.Run();

        // Assert
        act.ShouldThrow<PolicyViolationException>();
        _reporter.Received().Error(Arg.Is<string>(s => s.Contains("msg1")));
        _reporter.Received().Error(Arg.Is<string>(s => s.Contains("msg2")));
        await _executor.DidNotReceive().Apply(Arg.Any<MigrationPlan>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Run_ExecutorThrows_ReportsErrorAndRethrows()
    {
        // Arrange
        _executor
            .Apply(Arg.Any<MigrationPlan>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("boom"));

        // Act
        var act = async () => await _sut.Run();

        // Assert
        act.ShouldThrow<InvalidOperationException>();
        _reporter.Received().Error(Arg.Is<string>(s => s.Contains("boom")));
    }
}
