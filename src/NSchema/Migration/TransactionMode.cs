namespace NSchema.Migration;

/// <summary>
/// Controls how the SQL executor wraps a migration in a database transaction.
/// </summary>
public enum TransactionMode
{
    /// <summary>
    /// Run the entire migration inside a single transaction.
    /// </summary>
    /// <remarks>
    /// Statements explicitly marked with <see cref="SqlStatement.RunOutsideTransaction"/> are executed outside
    /// the transaction (the executor commits the open transaction, runs the statement, and opens a new transaction
    /// for any subsequent in-transaction statements).
    /// </remarks>
    Single,

    /// <summary>
    /// Run each statement on its own connection with no transaction.
    /// </summary>
    None,
}
