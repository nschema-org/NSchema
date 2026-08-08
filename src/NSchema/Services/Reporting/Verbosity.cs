namespace NSchema.Services.Reporting;

/// <summary>
/// How much of the run's output to show, selected by <c>--quiet</c> / <c>--verbose</c>.
/// </summary>
internal sealed class Verbosity
{
    /// <summary>
    /// Only shows outcomes, warnings, and summarized artifacts.
    /// </summary>
    public static readonly Verbosity Quiet = new(nameof(Quiet), summarizeArtifacts: true, kind => kind is MessageKind.Success or MessageKind.Warning);

    /// <summary>
    /// The default: everything except verbose detail.
    /// </summary>
    public static readonly Verbosity Normal = new(nameof(Normal), summarizeArtifacts: false, kind => kind is not MessageKind.Verbose);

    /// <summary>
    /// Everything, including verbose diagnostic detail.
    /// </summary>
    public static readonly Verbosity Verbose = new(nameof(Verbose), summarizeArtifacts: false, _ => true);

    private readonly string _name;
    private readonly Func<MessageKind, bool> _shouldShow;

    private Verbosity(string name, bool summarizeArtifacts, Func<MessageKind, bool> shouldShow)
    {
        _name = name;
        SummarizeArtifacts = summarizeArtifacts;
        _shouldShow = shouldShow;
    }

    /// <summary>
    /// Whether a narration message of the given kind should be printed.
    /// </summary>
    public bool ShouldShow(MessageKind kind) => _shouldShow(kind);

    /// <summary>
    /// Whether artifacts render their one-line summary face instead of their full rendering.
    /// </summary>
    public bool SummarizeArtifacts { get; }

    public override string ToString() => _name;
}
