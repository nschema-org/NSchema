using System.Data;
using System.Data.Common;
using Microsoft.Extensions.Options;

namespace NSchema.Migration.Sql;

/// <summary>
/// A default implementation of the ISqlExecutor interface that executes SQL statements using a provided DbDataSource.
/// </summary>
/// <param name="dataSource">The DbDataSource used to obtain database connections for executing SQL statements.</param>
/// <param name="options">Migration options that control how the executor handles transactions.</param>
public sealed class DefaultSqlExecutor(DbDataSource dataSource, IOptions<MigrationOptions>? options = null) : ISqlExecutor
{
    private readonly MigrationOptions _options = options?.Value ?? new MigrationOptions();

    /// <inheritdoc/>
    public async Task Execute(SqlPlan plan, CancellationToken cancellationToken = default)
    {
        if (plan.IsEmpty)
        {
            return;
        }

        await using var conn = await dataSource.OpenConnectionAsync(cancellationToken);

        if (_options.TransactionMode == TransactionMode.None)
        {
            foreach (var statement in plan.Statements)
            {
                await ExecuteOn(conn, transaction: null, statement.Sql, cancellationToken);
            }
            return;
        }

        DbTransaction? tx = null;
        try
        {
            foreach (var statement in plan.Statements)
            {
                if (statement.RunOutsideTransaction)
                {
                    if (tx is not null)
                    {
                        await tx.CommitAsync(cancellationToken);
                        await tx.DisposeAsync();
                        tx = null;
                    }
                    await ExecuteOn(conn, transaction: null, statement.Sql, cancellationToken);
                }
                else
                {
                    tx ??= await conn.BeginTransactionAsync(IsolationLevel.Unspecified, cancellationToken);
                    await ExecuteOn(conn, tx, statement.Sql, cancellationToken);
                }
            }

            if (tx is not null)
            {
                await tx.CommitAsync(cancellationToken);
                await tx.DisposeAsync();
            }
        }
        catch
        {
            if (tx is not null)
            {
                try { await tx.RollbackAsync(CancellationToken.None); } catch { /* swallow rollback failure */ }
                await tx.DisposeAsync();
            }
            throw;
        }
    }

    private static async Task ExecuteOn(DbConnection conn, DbTransaction? transaction, string sql, CancellationToken cancellationToken)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        if (transaction is not null)
        {
            cmd.Transaction = transaction;
        }

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}
