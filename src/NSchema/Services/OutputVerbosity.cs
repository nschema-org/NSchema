using NSchema.Operations;

namespace NSchema.Services;

/// <summary>
/// How much of the run narration to show, selected by <c>--quiet</c> / <c>--verbose</c>.
/// </summary>
internal enum Verbosity
{
    /// <summary>
    /// Only outcomes and warnings — suppress announcements, progress, and verbose detail.
    /// </summary>
    Quiet,

    /// <summary>
    /// The default: everything except verbose detail.
    /// </summary>
    Normal,

    /// <summary>
    /// Everything, including verbose diagnostic detail.
    /// </summary>
    Verbose,
}

/// <summary>
/// Decides which <see cref="MessageKind"/> line-messages a reporter should print, given the selected
/// <see cref="Verbosity"/>. Only the free-text narration (<see cref="IOperationReporter.Report"/>) is gated;
/// the structured artifacts (diff, SQL, schema, diagnostics, exceptions) are always shown, since they are the
/// actual results rather than chatter.
/// </summary>
internal sealed class OutputVerbosity(Verbosity level)
{
    public Verbosity Level { get; } = level;

    /// <summary>
    /// Whether a message of the given kind should be printed. The mapping is explicit rather than a numeric
    /// threshold so it doesn't depend on the declaration order of <see cref="MessageKind"/> in the core package.
    /// </summary>
    public bool ShouldShow(MessageKind kind) => Level switch
    {
        Verbosity.Quiet => kind is MessageKind.Success or MessageKind.Warning,
        Verbosity.Verbose => true,
        _ => kind is not MessageKind.Verbose,
    };
}
