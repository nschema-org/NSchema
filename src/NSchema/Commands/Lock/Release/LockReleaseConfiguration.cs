using System.CommandLine;
using NSchema.Configuration.Binding;
using NSchema.Configuration;
using NSchema.Configuration.State;

namespace NSchema.Commands.Lock.Release;

/// <summary>
/// Configuration for the <c>lock release</c> command.
/// </summary>
internal sealed class LockReleaseConfiguration : IBindable
{
    /// <summary>
    /// The state store whose lock is released. Releasing only touches the lock and never contacts the live database.
    /// </summary>
    public StateConfig? State { get; set; }

    /// <summary>
    /// The id of the lock to release. Required unless <see cref="Force"/> is set.
    /// </summary>
    public string? LockId { get; set; }

    /// <summary>
    /// Whether to release whatever lock is held without naming its id.
    /// </summary>
    public bool Force { get; set; }

    /// <summary>
    /// Whether to skip the interactive confirmation prompt before releasing the lock.
    /// </summary>
    public bool AutoApprove { get; private set; }

    public void Bind(ProjectConfig project, ParseResult cli)
    {
        State = project.State;
        LockId = cli.GetValue(LockReleaseCommand.LockIdArgument);
        LockReleaseOptions.Force.Bind(cli, f => Force = f);
        LockReleaseOptions.AutoApprove.Bind(cli, a => AutoApprove = a);
    }
}
