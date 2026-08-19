namespace NSchema.Apply;

/// <summary>
/// Configures SQL options.
/// </summary>
internal class SqlOptions
{
    /// <summary>
    /// Controls whether the migration runs inside a transaction.
    /// </summary>
    public TransactionMode TransactionMode { get; set; } = TransactionMode.Single;
}
