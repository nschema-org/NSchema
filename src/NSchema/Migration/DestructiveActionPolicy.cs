namespace NSchema.Migration;

/// <summary>
/// Defines policies for handling destructive actions during database schema migrations.
/// </summary>
public enum DestructiveActionPolicy
{
    /// <summary>
    /// Indicates that destructive actions are not allowed and will result in an error if encountered during migration.
    /// </summary>
    Error,

    /// <summary>
    /// Indicates that destructive actions are allowed but will trigger a warning log entry when encountered during migration, allowing the migration to proceed while notifying the user of potential issues.
    /// </summary>
    Warn,

    /// <summary>
    /// Indicates that destructive actions are allowed without any warnings or errors, allowing the migration to proceed regardless of the presence of destructive actions.
    /// </summary>
    Allow,
}
