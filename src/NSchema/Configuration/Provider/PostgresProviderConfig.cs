namespace NSchema.Configuration.Provider;

/// <summary>
/// Configures the PostgreSQL database provider.
/// </summary>
internal sealed class PostgresProviderConfig
{
    /// <summary>
    /// The connection string used to reach the database.
    /// </summary>
    public string ConnectionString { get; set; } = "";

    /// <summary>
    /// The command timeout, in seconds. When null, Npgsql's default is used.
    /// </summary>
    public int? CommandTimeout { get; set; }
}
