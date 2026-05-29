using NSchema.Hosting;
using NSchema.Migration;
using NSchema.Migration.Plan;
using NSchema.Migration.Sql;
using NSchema.Schema;
using NSubstitute.ExceptionExtensions;

namespace NSchema.Tests.Migration.Sql;

public sealed class SqlMigrationExecutorTests
{
    private readonly IMigrationReporter _reporter = Substitute.For<IMigrationReporter>();
    private readonly ISqlPlanner _sqlPlanner = Substitute.For<ISqlPlanner>();
    private readonly ISqlExecutor _sqlExecutor = Substitute.For<ISqlExecutor>();

    private readonly SqlMigrationExecutor _sut;

    public SqlMigrationExecutorTests()
    {
        _sqlPlanner.Plan(Arg.Any<MigrationPlan>()).Returns(new SqlPlan([]));
        _sut = new SqlMigrationExecutor(_reporter, _sqlPlanner, _sqlExecutor);
    }

    private static MigrationPlan EmptyMigrationPlan() => new([], DatabaseSchema.Create([]));

    [Fact]
    public async Task Apply_PassesMigrationPlanToSqlPlanner()
    {
        // Arrange
        var plan = EmptyMigrationPlan();

        // Act
        await _sut.Apply(plan, planOnly: false);

        // Assert
        _sqlPlanner.Received(1).Plan(plan);
    }

    [Fact]
    public async Task Apply_NotDryRun_ExecutesSqlPlan()
    {
        // Arrange
        var sqlPlan = new SqlPlan([new SqlStatement("SELECT 1")]);
        _sqlPlanner.Plan(Arg.Any<MigrationPlan>()).Returns(sqlPlan);

        // Act
        await _sut.Apply(EmptyMigrationPlan(), planOnly: false);

        // Assert
        await _sqlExecutor.Received(1).Execute(sqlPlan, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Apply_DryRun_DoesNotExecuteSqlPlan()
    {
        // Arrange

        // Act
        await _sut.Apply(EmptyMigrationPlan(), planOnly: true);

        // Assert
        await _sqlExecutor.DidNotReceive().Execute(Arg.Any<SqlPlan>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Apply_ReportsEachStatement()
    {
        // Arrange
        var sqlPlan = new SqlPlan([
            new SqlStatement("CREATE SCHEMA app"),
            new SqlStatement("CREATE TABLE app.users (id int)"),
        ]);
        _sqlPlanner.Plan(Arg.Any<MigrationPlan>()).Returns(sqlPlan);

        // Act
        await _sut.Apply(EmptyMigrationPlan(), planOnly: false);

        // Assert
        _reporter.Received(1).Info("CREATE SCHEMA app");
        _reporter.Received(1).Info("CREATE TABLE app.users (id int)");
    }

    [Fact]
    public async Task Apply_DryRun_StillReportsStatements()
    {
        // Arrange
        var sqlPlan = new SqlPlan([new SqlStatement("CREATE SCHEMA app")]);
        _sqlPlanner.Plan(Arg.Any<MigrationPlan>()).Returns(sqlPlan);

        // Act
        await _sut.Apply(EmptyMigrationPlan(), planOnly: true);

        // Assert
        _reporter.Received(1).Info("CREATE SCHEMA app");
    }

    [Fact]
    public async Task Apply_PassesCancellationTokenToExecutor()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        // Act
        await _sut.Apply(EmptyMigrationPlan(), planOnly: false, token);

        // Assert
        await _sqlExecutor.Received(1).Execute(Arg.Any<SqlPlan>(), token);
    }

    [Fact]
    public async Task Apply_PlansSqlBeforeExecuting()
    {
        // Arrange
        var sqlPlan = new SqlPlan([new SqlStatement("SELECT 1")]);
        _sqlPlanner.Plan(Arg.Any<MigrationPlan>()).Returns(sqlPlan);

        // Act
        await _sut.Apply(EmptyMigrationPlan(), planOnly: false);

        // Assert
        Received.InOrder(() =>
        {
            _sqlPlanner.Plan(Arg.Any<MigrationPlan>());
            _sqlExecutor.Execute(sqlPlan, Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task Apply_ExecutorThrows_PropagatesException()
    {
        // Arrange
        _sqlExecutor.Execute(Arg.Any<SqlPlan>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("boom"));

        // Act
        var act = () => _sut.Apply(EmptyMigrationPlan(), planOnly: false);

        // Assert
        await Should.ThrowAsync<InvalidOperationException>(act);
    }
}
