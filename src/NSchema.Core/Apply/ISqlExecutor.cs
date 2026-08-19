using NSchema.Plan.Domain;

namespace NSchema.Apply;

/// <summary>
/// Defines an interface for executing a plan's SQL statements.
/// </summary>
internal interface ISqlExecutor
{
    /// <summary>
    /// Executes the given statements in order, applying the necessary changes to the database schema.
    /// </summary>
    /// <param name="statements">The ordered SQL statements to execute.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests. If the operation is canceled, execution should stop gracefully.</param>
    /// <returns>A task that represents the asynchronous execution. The task completes when every statement has been executed successfully.</returns>
    Task Execute(IReadOnlyList<SqlStatement> statements, CancellationToken cancellationToken = default);
}
