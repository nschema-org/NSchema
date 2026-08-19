namespace NSchema.Project.Nsql.Syntax.Triggers;

/// <summary>
/// A trigger's timing keyword.
/// </summary>
public enum TriggerTiming
{
    /// <summary>
    /// <c>BEFORE</c>.
    /// </summary>
    Before,

    /// <summary>
    /// <c>AFTER</c>.
    /// </summary>
    After,

    /// <summary>
    /// <c>INSTEAD OF</c>.
    /// </summary>
    InsteadOf
}
