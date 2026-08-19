namespace NSchema.Model.Triggers;

/// <summary>
/// When a trigger fires relative to the statement it is attached to.
/// </summary>
public enum TriggerTiming
{
    /// <summary>
    /// Fires before the operation.
    /// </summary>
    Before,

    /// <summary>
    /// Fires after the operation.
    /// </summary>
    After,

    /// <summary>
    /// Fires instead of the operation (views).
    /// </summary>
    InsteadOf
}
