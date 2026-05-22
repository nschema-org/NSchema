namespace NSchema.Migration.ScriptProviders;

/// <summary>
/// Represents the phase of deployment for which a migration script is intended.
/// </summary>
internal enum DeploymentPhase
{
    /// <summary>
    /// Indicates that the migration script is intended to be executed before the main deployment process.
    /// </summary>
    Pre,

    /// <summary>
    /// Indicates that the migration script is intended to be executed after the main deployment process.
    /// </summary>
    Post,
}
