using System.Data;
using System.Data.Common;
using Microsoft.Extensions.Options;
using NSchema.Model;
using NSchema.Plan.Domain;

namespace NSchema.Apply;

/// <summary>
/// A default implementation of the ISqlExecutor interface that executes SQL statements using a provided DbDataSource.
/// </summary>
/// <param name="options">Options that control how the executor handles transactions.</param>
/// <param name="dataSource">The DbDataSource used to obtain database connections.</param>
internal sealed class SqlExecutor(IOptions<SqlOptions> options, DbDataSource? dataSource = null) : ISqlExecutor
{
    /// <inheritdoc/>
    public async Task Execute(IReadOnlyList<SqlStatement> statements, CancellationToken cancellationToken = default)
    {
        if (statements.Count == 0)
        {
            return;
        }

        if (dataSource is null)
        {
            throw new InvalidOperationException("Cannot execute the migration: no database connection is configured. Register a database provider.");
        }

        await using var conn = await dataSource.OpenConnectionAsync(cancellationToken);

        if (options.Value.TransactionMode == TransactionMode.None)
        {
            foreach (var statement in statements)
            {
                await ExecuteOn(conn, transaction: null, statement.Sql, cancellationToken);
            }
            return;
        }

        DbTransaction? tx = null;
        try
        {
            foreach (var statement in statements)
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

    private static async Task ExecuteOn(DbConnection conn, DbTransaction? transaction, SqlText sql, CancellationToken cancellationToken)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql.Value;
        if (transaction is not null)
        {
            cmd.Transaction = transaction;
        }

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}
