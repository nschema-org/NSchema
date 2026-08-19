namespace NSchema.Model.Triggers;

/// <summary>
/// The operations that fire a trigger.
/// </summary>
[Flags]
public enum TriggerEvent
{
    /// <summary>
    /// No event (not a valid trigger on its own).
    /// </summary>
    None = 0,

    /// <summary>
    /// Fires on <c>INSERT</c>.
    /// </summary>
    Insert = 1,

    /// <summary>
    /// Fires on <c>UPDATE</c> (optionally narrowed to specific columns; see <see cref="Trigger.UpdateOfColumns"/>).
    /// </summary>
    Update = 2,

    /// <summary>
    /// Fires on <c>DELETE</c>.
    /// </summary>
    Delete = 4,

    /// <summary>
    /// Fires on <c>TRUNCATE</c>.
    /// </summary>
    Truncate = 8
}
