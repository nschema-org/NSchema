namespace NSchema.Configuration;

/// <summary>
/// The environment variables the CLI reads as configuration overrides.
/// </summary>
internal static class EnvironmentVariables
{
    /// <summary>
    /// The connection string for the PostgreSQL provider. Self-identifying: it fills <c>provider.postgres</c>, so no
    /// separate provider discriminator is needed (mirroring how <c>state.s3</c> is named by its own settings).
    /// </summary>
    public const string PostgresConnectionString = "NSCHEMA_POSTGRES_CONNECTION_STRING";

    /// <summary>
    /// The username for the PostgreSQL provider.
    /// </summary>
    public const string PostgresUsername = "NSCHEMA_POSTGRES_USERNAME";

    /// <summary>
    /// The password for the PostgreSQL provider`.
    /// </summary>
    public const string PostgresPassword = "NSCHEMA_POSTGRES_PASSWORD";

    /// <summary>
    /// The connection string for the SQLite provider, e.g. <c>Data Source=app.db</c>.
    /// </summary>
    public const string SqliteConnectionString = "NSCHEMA_SQLITE_CONNECTION_STRING";

    /// <summary>
    /// The policy applied when the plan contains destructive actions.
    /// </summary>
    public const string DestructiveActionPolicy = "NSCHEMA_DESTRUCTIVE_ACTION_POLICY";

    /// <summary>
    /// The environment to target. Selects the <c>*.env.&lt;name&gt;.sql</c> overlay files layered over the base configuration.
    /// </summary>
    public const string Environment = "NSCHEMA_ENVIRONMENT";

    /// <summary>
    /// The well-known <c>NO_COLOR</c> convention (https://no-color.org).
    /// </summary>
    public const string NoColor = "NO_COLOR";

    /// <summary>
    /// The conventional <c>COLUMNS</c> terminal width.
    /// </summary>
    public const string Columns = "COLUMNS";
}
