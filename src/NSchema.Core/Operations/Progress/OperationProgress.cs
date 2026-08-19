namespace NSchema.Operations.Progress;

/// <summary>
/// A single unit of transient progress narration emitted while an operation runs.
/// </summary>
/// <param name="Level">How prominent the narration is.</param>
/// <param name="Message">The human-readable progress text.</param>
public readonly record struct OperationProgress(ProgressLevel Level, string Message)
{
    /// <summary>
    /// Creates a normal progress step.
    /// </summary>
    /// <param name="message">The progress text.</param>
    /// <returns>A <see cref="ProgressLevel.Step"/> progress item.</returns>
    public static OperationProgress Step(string message) => new(ProgressLevel.Step, message);

    /// <summary>
    /// Creates a verbose detail item.
    /// </summary>
    /// <param name="message">The detail text.</param>
    /// <returns>A <see cref="ProgressLevel.Detail"/> progress item.</returns>
    public static OperationProgress Detail(string message) => new(ProgressLevel.Detail, message);
}
