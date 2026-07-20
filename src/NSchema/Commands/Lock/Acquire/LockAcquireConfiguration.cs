using System.CommandLine;
using NSchema.Configuration;
using NSchema.Configuration.Binding;
using NSchema.Configuration.State;

namespace NSchema.Commands.Lock.Acquire;

/// <summary>
/// Configuration for the <c>lock acquire</c> command.
/// </summary>
internal sealed class LockAcquireConfiguration : IBindable
{
    /// <summary>
    /// The state store whose lock is taken. Acquiring only touches the lock and never contacts the live database.
    /// </summary>
    public StateConfig? State { get; set; }

    /// <summary>
    /// The note recorded as the lock's operation, surfaced by <c>lock status</c>. Defaults to <c>manual</c>.
    /// </summary>
    public string Reason { get; private set; } = "manual";

    /// <summary>
    /// The raw <c>--ttl</c> text, if any. Validated for parseability by the validator and turned into
    /// <see cref="TimeToLive"/> once valid.
    /// </summary>
    public string? TtlText { get; private set; }

    /// <summary>
    /// The parsed lifetime after which the lock is reported as expired, or <see langword="null"/> for no expiry.
    /// </summary>
    public TimeSpan? TimeToLive => TtlText is null ? null : Duration.Parse(TtlText);

    public void Bind(ProjectConfig project, ParseResult cli)
    {
        State = project.State;
        LockAcquireOptions.Reason.Bind(cli, r => Reason = r);
        LockAcquireOptions.Ttl.Bind(cli, t => TtlText = t);
    }
}
