namespace NSchema.Project.Nsql.Syntax.Triggers;

/// <summary>
/// The events a trigger fires on.
/// </summary>
[Flags]
public enum TriggerEvent
{
    /// <summary>
    /// No event.
    /// </summary>
    None = 0,

    /// <summary>
    /// <c>INSERT</c>.
    /// </summary>
    Insert = 1,

    /// <summary>
    /// <c>UPDATE</c>.
    /// </summary>
    Update = 2,

    /// <summary>
    /// <c>DELETE</c>.
    /// </summary>
    Delete = 4,

    /// <summary>
    /// <c>TRUNCATE</c>.
    /// </summary>
    Truncate = 8
}
