namespace NSchema.Operations.Progress;

/// <summary>
/// An <see cref="IProgress{T}"/> sink that discards progress.
/// </summary>
internal sealed class NullOperationProgress : IProgress<OperationProgress>
{
    public void Report(OperationProgress value)
    {
        // Intentionally empty: progress is transient narration a headless caller does not need.
    }
}
