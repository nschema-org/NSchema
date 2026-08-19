namespace NSchema.Model.Scripts;

/// <summary>
/// When a script runs, relative to occurrences of its event.
/// </summary>
public enum RunCondition
{
    /// <summary>
    /// The script runs every time its event occurs.
    /// </summary>
    Always,

    /// <summary>
    /// The script runs at most once: after it has been recorded as executed, later occurrences of its event skip it.
    /// </summary>
    Once
}
