namespace NSchema.Cli.Configuration;

/// <summary>
/// The environment variables the CLI reads as configuration overrides.
/// </summary>
internal static class EnvironmentVariables
{
    /// <summary>
    /// Selects the database provider (e.g. <c>postgres</c>).
    /// </summary>
    public const string Provider = "NSCHEMA_PROVIDER";

    /// <summary>
    /// The connection string for the database provider.
    /// </summary>
    public const string ConnectionString = "NSCHEMA_CONNECTION_STRING";

    /// <summary>
    /// The path for a file state store.
    /// </summary>
    public const string StateFile = "NSCHEMA_STATE_FILE";

    /// <summary>
    /// The bucket for an S3 state store.
    /// </summary>
    public const string StateS3Bucket = "NSCHEMA_STATE_S3_BUCKET";

    /// <summary>
    /// The object key for an S3 state store.
    /// </summary>
    public const string StateS3Key = "NSCHEMA_STATE_S3_KEY";

    /// <summary>
    /// The policy applied when the plan contains destructive actions.
    /// </summary>
    public const string DestructiveActionPolicy = "NSCHEMA_DESTRUCTIVE_ACTION_POLICY";
}
