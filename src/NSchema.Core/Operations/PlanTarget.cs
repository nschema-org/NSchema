namespace NSchema.Operations;

/// <summary>
/// The state a plan converges towards.
/// </summary>
public enum PlanTarget
{
    /// <summary>
    /// The schema declared by the project.
    /// </summary>
    Project,

    /// <summary>
    /// Plan using an empty database as the target state. Used for performing a teardown.
    /// </summary>
    Empty
}
