namespace NSchema.Cli.Configuration;

/// <summary>
/// Configures the PostgreSQL database provider.
/// </summary>
internal sealed class PostgresProviderConfig
{
    /// <summary>
    /// The connection string used to reach the database.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// The command timeout, in seconds. When null, Npgsql's default is used.
    /// </summary>
    public int? CommandTimeout { get; set; }

    /// <summary>
    /// Validates the configuration, yielding one message per problem found.
    /// </summary>
    public IEnumerable<string> Validate()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            yield return $"provider.postgres.connectionString is required. Set it in nschema.json, --connection-string, or {EnvironmentVariables.ConnectionString}.";
        }

        if (CommandTimeout is < 0)
        {
            yield return "provider.postgres.commandTimeout must not be negative.";
        }
    }
}