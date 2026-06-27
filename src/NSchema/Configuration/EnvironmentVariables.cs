namespace NSchema.Configuration;

/// <summary>
/// The environment variables the CLI reads.
/// </summary>
internal static class EnvironmentVariables
{
    /// <summary>
    /// The connection string for the PostgreSQL provider — the variable <c>init</c> points the user at. The plugin
    /// (not the CLI) reads it at runtime.
    /// </summary>
    public const string PostgresConnectionString = "NSCHEMA_POSTGRES_CONNECTION_STRING";

    /// <summary>
    /// The connection string for the SQLite provider, e.g. <c>Data Source=app.db</c>.
    /// </summary>
    public const string SqliteConnectionString = "NSCHEMA_SQLITE_CONNECTION_STRING";

    /// <summary>
    /// The connection string for the SQL Server provider.
    /// </summary>
    public const string SqlServerConnectionString = "NSCHEMA_SQLSERVER_CONNECTION_STRING";

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
