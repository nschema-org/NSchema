namespace NSchema.Configuration;

/// <summary>
/// The environment variables the CLI reads.
/// </summary>
internal static class EnvironmentVariables
{
    /// <summary>
    /// The connection string for the configured database — the variable <c>new</c> points the user at.
    /// </summary>
    public const string DatabaseConnectionString = "NSCHEMA_DATABASE_CONNECTION_STRING";

    /// <summary>
    /// The policy applied when the plan contains destructive actions.
    /// </summary>
    public const string DestructiveActionPolicy = "NSCHEMA_DESTRUCTIVE_ACTION_POLICY";

    /// <summary>
    /// The policy applied when the plan contains changes that can fail on existing data.
    /// </summary>
    public const string DataHazardPolicy = "NSCHEMA_DATA_HAZARD_POLICY";

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
