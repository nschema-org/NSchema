using NSchema.Migration.Plan;
using NSchema.Migration.Sql;
using NSchema.Schema;
using NSubstitute.ExceptionExtensions;

namespace NSchema.Tests.Migration.Sql;

public sealed class SqlMigrationCompilerTests
{
    private readonly ISqlPlanner _sqlPlanner = Substitute.For<ISqlPlanner>();
    private readonly ISqlExecutor _sqlExecutor = Substitute.For<ISqlExecutor>();

    private readonly SqlMigrationCompiler _sut;

    public SqlMigrationCompilerTests()
    {
        _sqlPlanner.Plan(Arg.Any<MigrationPlan>()).Returns(new SqlPlan([]));
        _sut = new SqlMigrationCompiler(_sqlPlanner, _sqlExecutor);
    }

    private static MigrationPlan EmptyMigrationPlan() => new([], DatabaseSchema.Create([]));

    [Fact]
    public async Task Compile_PassesMigrationPlanToSqlPlanner()
    {
        // Arrange
        var plan = EmptyMigrationPlan();

        // Act
        await _sut.Compile(plan);

        // Assert
        _sqlPlanner.Received(1).Plan(plan);
    }

    [Fact]
    public async Task Compile_DoesNotExecute()
    {
        // Arrange

        // Act
        await _sut.Compile(EmptyMigrationPlan());

        // Assert
        await _sqlExecutor.DidNotReceive().Execute(Arg.Any<SqlPlan>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Preview_ContainsEachStatement()
    {
        // Arrange
        var sqlPlan = new SqlPlan([
            new SqlStatement("CREATE SCHEMA app"),
            new SqlStatement("CREATE TABLE app.users (id int)"),
        ]);
        _sqlPlanner.Plan(Arg.Any<MigrationPlan>()).Returns(sqlPlan);

        // Act
        var execution = await _sut.Compile(EmptyMigrationPlan());

        // Assert
        execution.Preview.ShouldBe(["CREATE SCHEMA app", "CREATE TABLE app.users (id int)"]);
    }

    [Fact]
    public async Task Execute_ExecutesSqlPlan()
    {
        // Arrange
        var sqlPlan = new SqlPlan([new SqlStatement("SELECT 1")]);
        _sqlPlanner.Plan(Arg.Any<MigrationPlan>()).Returns(sqlPlan);

        // Act
        var execution = await _sut.Compile(EmptyMigrationPlan());
        await execution.Execute();

        // Assert
        await _sqlExecutor.Received(1).Execute(sqlPlan, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_PassesCancellationTokenToExecutor()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        // Act
        var execution = await _sut.Compile(EmptyMigrationPlan());
        await execution.Execute(token);

        // Assert
        await _sqlExecutor.Received(1).Execute(Arg.Any<SqlPlan>(), token);
    }

    [Fact]
    public async Task Execute_ExecutorThrows_PropagatesException()
    {
        // Arrange
        _sqlExecutor.Execute(Arg.Any<SqlPlan>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("boom"));

        // Act
        var execution = await _sut.Compile(EmptyMigrationPlan());
        var act = () => execution.Execute();

        // Assert
        await Should.ThrowAsync<InvalidOperationException>(act);
    }
}
