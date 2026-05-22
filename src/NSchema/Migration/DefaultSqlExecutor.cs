using System.Data.Common;

namespace NSchema.Migration;

/// <summary>
/// A default implementation of the ISqlExecutor interface that executes SQL statements using a provided DbDataSource.
/// </summary>
/// <param name="dataSource">The DbDataSource used to obtain database connections for executing SQL statements.</param>
public sealed class DefaultSqlExecutor(DbDataSource dataSource) : ISqlExecutor
{
    /// <inheritdoc/>
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
