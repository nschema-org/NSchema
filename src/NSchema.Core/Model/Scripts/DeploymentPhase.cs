namespace NSchema.Model.Scripts;

/// <summary>
/// Where a deployment-event script runs relative to the migration actions.
/// </summary>
public enum DeploymentPhase
{
    /// <summary>
    /// The script runs before the migration actions.
    /// </summary>
    Pre,

    /// <summary>
    /// The script runs after the migration actions.
    /// </summary>
    Post
}
