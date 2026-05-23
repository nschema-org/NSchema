// using Microsoft.Extensions.Options;
// using NSchema.Migration;
//
// namespace NSchema.Tests.Migration;
//
// [Collection("postgres")]
// public sealed class DefaultSqlExecutorTests(PostgresContainerFixture fixture) : IAsyncLifetime
// {
//     private readonly NpgsqlDataSource _dataSource = fixture.DataSource;
//     private readonly string _schema = $"exec_{Guid.NewGuid():N}";
//
//     public async Task InitializeAsync()
//     {
//         await Exec($"""CREATE SCHEMA "{_schema}" """);
//         await Exec($"""CREATE TABLE "{_schema}"."t" (id int)""");
//     }
//
//     public async Task DisposeAsync()
//     {
//         await Exec($"""DROP SCHEMA IF EXISTS "{_schema}" CASCADE""");
//     }
//
//     private static DefaultSqlExecutor Executor(NpgsqlDataSource source, MigrationOptions? opts = null)
//         => new(source, Options.Create(opts ?? new MigrationOptions()));
//
//     private async Task<long> RowCount()
//     {
//         await using var conn = await _dataSource.OpenConnectionAsync();
//         await using var cmd = conn.CreateCommand();
//         cmd.CommandText = $"""SELECT COUNT(*) FROM "{_schema}"."t" """;
//         return (long)(await cmd.ExecuteScalarAsync() ?? 0L);
//     }
//
//     private async Task Exec(string sql)
//     {
//         await using var conn = await _dataSource.OpenConnectionAsync();
//         await using var cmd = conn.CreateCommand();
//         cmd.CommandText = sql;
//         await cmd.ExecuteNonQueryAsync();
//     }
//
//     [Fact]
//     public async Task Execute_EmptyPlan_DoesNothing()
//     {
//         await Executor(_dataSource).Execute(new SqlPlan([]));
//         (await RowCount()).ShouldBe(0);
//     }
//
//     [Fact]
//     public async Task Execute_SingleTransactionMode_CommitsAllStatementsOnSuccess()
//     {
//         var plan = new SqlPlan([
//             new SqlStatement($"""INSERT INTO "{_schema}"."t" (id) VALUES (1)"""),
//             new SqlStatement($"""INSERT INTO "{_schema}"."t" (id) VALUES (2)"""),
//         ]);
//
//         await Executor(_dataSource, new MigrationOptions { TransactionMode = TransactionMode.Single }).Execute(plan);
//
//         (await RowCount()).ShouldBe(2);
//     }
//
//     [Fact]
//     public async Task Execute_SingleTransactionMode_RollsBackOnFailure()
//     {
//         var plan = new SqlPlan([
//             new SqlStatement($"""INSERT INTO "{_schema}"."t" (id) VALUES (1)"""),
//             new SqlStatement("SELECT * FROM nonexistent_table_xyz"),
//         ]);
//
//         await Should.ThrowAsync<PostgresException>(() =>
//             Executor(_dataSource, new MigrationOptions { TransactionMode = TransactionMode.Single }).Execute(plan));
//
//         (await RowCount()).ShouldBe(0);
//     }
//
//     [Fact]
//     public async Task Execute_NoTransactionMode_DoesNotRollBackOnFailure()
//     {
//         var plan = new SqlPlan([
//             new SqlStatement($"""INSERT INTO "{_schema}"."t" (id) VALUES (1)"""),
//             new SqlStatement("SELECT * FROM nonexistent_table_xyz"),
//         ]);
//
//         await Should.ThrowAsync<PostgresException>(() =>
//             Executor(_dataSource, new MigrationOptions { TransactionMode = TransactionMode.None }).Execute(plan));
//
//         // First insert was auto-committed before the failure.
//         (await RowCount()).ShouldBe(1);
//     }
//
//     [Fact]
//     public async Task Execute_RunOutsideTransaction_CommitsPriorStatementsThenRunsOutside()
//     {
//         var plan = new SqlPlan([
//             new SqlStatement($"""INSERT INTO "{_schema}"."t" (id) VALUES (1)"""),
//             // RunOutsideTransaction forces the prior tx to commit even if a later
//             // statement fails, so the first insert must survive the rollback below.
//             new SqlStatement($"""INSERT INTO "{_schema}"."t" (id) VALUES (2)""", RunOutsideTransaction: true),
//             new SqlStatement($"""INSERT INTO "{_schema}"."t" (id) VALUES (3)"""),
//             new SqlStatement("SELECT * FROM nonexistent_table_xyz"),
//         ]);
//
//         await Should.ThrowAsync<PostgresException>(() =>
//             Executor(_dataSource, new MigrationOptions { TransactionMode = TransactionMode.Single }).Execute(plan));
//
//         // 1 and 2 survive (1 committed when RunOutsideTransaction encountered, 2 ran outside any tx).
//         // 3 was in the new tx that got rolled back by the failure on statement 4.
//         (await RowCount()).ShouldBe(2);
//     }
// }
