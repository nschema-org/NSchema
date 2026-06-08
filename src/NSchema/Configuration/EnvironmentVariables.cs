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
    /// The policy applied when the plan contains destructive actions.
    /// </summary>
    public const string DestructiveActionPolicy = "NSCHEMA_DESTRUCTIVE_ACTION_POLICY";

    /// <summary>
    /// The well-known <c>NO_COLOR</c> convention (https://no-color.org).
    /// </summary>
    public const string NoColor = "NO_COLOR";
}
