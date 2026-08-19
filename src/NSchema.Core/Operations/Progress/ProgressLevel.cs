namespace NSchema.Operations.Progress;

/// <summary>
/// How prominent a piece of <see cref="OperationProgress"/> narration is.
/// </summary>
public enum ProgressLevel
{
    /// <summary>
    /// A normal progress step worth showing by default (e.g. "Computing migration plan...").
    /// </summary>
    Step,

    /// <summary>
    /// Low-level detail of interest only when extra verbosity is requested (e.g. the list of files read).
    /// </summary>
    Detail
}
