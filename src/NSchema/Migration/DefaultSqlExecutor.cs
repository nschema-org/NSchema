using System.Data.Common;

namespace NSchema.Migration;

internal sealed class DefaultSqlExecutor(DbDataSource dataSource) : ISqlExecutor
{
    public async Task Execute(SqlPlan plan, CancellationToken cancellationToken = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(cancellationToken);

        foreach (string statement in plan.Statements)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = statement;
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
