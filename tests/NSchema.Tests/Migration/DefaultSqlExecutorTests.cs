using Microsoft.Extensions.Options;
using Npgsql;
using NSchema.Migration;
using NSchema.Migration.Sql;
using NSchema.Tests.Fixtures;

namespace NSchema.Tests.Migration;

[Collection("postgres")]
public sealed class DefaultSqlExecutorTests : IAsyncLifetime
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly string _schema = $"exec_{Guid.NewGuid():N}";
    private readonly IOptions<MigrationOptions> _options = Options.Create(new MigrationOptions());

    private readonly DefaultSqlExecutor _sut;

    public DefaultSqlExecutorTests(PostgresContainerFixture fixture)
    {
        _dataSource = fixture.DataSource;
        _sut = new DefaultSqlExecutor(_dataSource, _options);
    }

    public async Task InitializeAsync()
    {
        await Exec($"""CREATE SCHEMA "{_schema}" """);
        await Exec($"""CREATE TABLE "{_schema}"."t" (id int)""");
    }

    [Fact]
    public async Task Execute_EmptyPlan_DoesNothing()
    {
        // Arrange
        var plan = new SqlPlan([]);

        // Act
        await _sut.Execute(plan);

        // Assert
        (await RowCount()).ShouldBe(0);
    }

    [Fact]
    public async Task Execute_SingleTransactionMode_CommitsAllStatementsOnSuccess()
    {
        // Arrange
        _options.Value.TransactionMode = TransactionMode.Single;
        var plan = new SqlPlan([
            new SqlStatement($"""INSERT INTO "{_schema}"."t" (id) VALUES (1)"""),
            new SqlStatement($"""INSERT INTO "{_schema}"."t" (id) VALUES (2)"""),
        ]);

        // Act
        await _sut.Execute(plan);

        // Assert
        (await RowCount()).ShouldBe(2);
    }

    [Fact]
    public async Task Execute_SingleTransactionMode_RollsBackOnFailure()
    {
        // Arrange
        _options.Value.TransactionMode = TransactionMode.Single;
        var plan = new SqlPlan([
            new SqlStatement($"""INSERT INTO "{_schema}"."t" (id) VALUES (1)"""),
            new SqlStatement("SELECT * FROM nonexistent_table_xyz"),
        ]);

        // Act
        var act = () => _sut.Execute(plan);

        // Assert
        await Should.ThrowAsync<PostgresException>(act);
        (await RowCount()).ShouldBe(0);
    }

    [Fact]
    public async Task Execute_NoTransactionMode_DoesNotRollBackOnFailure()
    {
        // Arrange
        _options.Value.TransactionMode = TransactionMode.None;
        var plan = new SqlPlan([
            new SqlStatement($"""INSERT INTO "{_schema}"."t" (id) VALUES (1)"""),
            new SqlStatement("SELECT * FROM nonexistent_table_xyz"),
        ]);

        // Act
        var act = () => _sut.Execute(plan);

        // Assert
        await Should.ThrowAsync<PostgresException>(act);
        // First insert was auto-committed before the failure.
        (await RowCount()).ShouldBe(1);
    }

    [Fact]
    public async Task Execute_RunOutsideTransaction_CommitsPriorStatementsThenRunsOutside()
    {
        // Arrange
        _options.Value.TransactionMode = TransactionMode.Single;
        var plan = new SqlPlan([
            new SqlStatement($"""INSERT INTO "{_schema}"."t" (id) VALUES (1)"""),
            // RunOutsideTransaction forces the prior tx to commit even if a later
            // statement fails, so the first insert must survive the rollback below.
            new SqlStatement($"""INSERT INTO "{_schema}"."t" (id) VALUES (2)""", RunOutsideTransaction: true),
            new SqlStatement($"""INSERT INTO "{_schema}"."t" (id) VALUES (3)"""),
            new SqlStatement("SELECT * FROM nonexistent_table_xyz"),
        ]);

        // Act
        var act = () => _sut.Execute(plan);

        // Assert
        await Should.ThrowAsync<PostgresException>(act);
        // 1 and 2 survive (1 committed when RunOutsideTransaction encountered, 2 ran outside any tx).
        // 3 was in the new tx that got rolled back by the failure on statement 4.
        (await RowCount()).ShouldBe(2);
    }

    public async Task DisposeAsync()
    {
        await Exec($"""DROP SCHEMA IF EXISTS "{_schema}" CASCADE""");
    }

    private async Task<long> RowCount()
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""SELECT COUNT(*) FROM "{_schema}"."t" """;
        return (long)(await cmd.ExecuteScalarAsync() ?? 0L);
    }

    private async Task Exec(string sql)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();
    }

}
