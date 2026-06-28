namespace NSchema.Services.Reporting;

/// <summary>
/// How much of the run narration to show, selected by <c>--quiet</c> / <c>--verbose</c>.
/// </summary>
internal sealed class Verbosity
{
    /// <summary>
    /// Only outcomes and warnings — suppress announcements, progress, and verbose detail.
    /// </summary>
    public static readonly Verbosity Quiet = new(nameof(Quiet), kind => kind is MessageKind.Success or MessageKind.Warning);

    /// <summary>
    /// The default: everything except verbose detail.
    /// </summary>
    public static readonly Verbosity Normal = new(nameof(Normal), kind => kind is not MessageKind.Verbose);

    /// <summary>
    /// Everything, including verbose diagnostic detail.
    /// </summary>
    public static readonly Verbosity Verbose = new(nameof(Verbose), _ => true);

    private readonly string _name;
    private readonly Func<MessageKind, bool> _shouldShow;

    private Verbosity(string name, Func<MessageKind, bool> shouldShow)
    {
        _name = name;
        _shouldShow = shouldShow;
    }

    /// <summary>
    /// Whether a message of the given kind should be printed.
    /// </summary>
    public bool ShouldShow(MessageKind kind) => _shouldShow(kind);

    public override string ToString() => _name;
}
