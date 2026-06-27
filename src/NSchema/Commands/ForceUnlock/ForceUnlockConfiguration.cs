using System.CommandLine;
using NSchema.Configuration.Binding;
using NSchema.Configuration.Ddl;
using NSchema.Configuration.State;

namespace NSchema.Commands.ForceUnlock;

/// <summary>
/// configuration for the force-unlock command.
/// </summary>
internal sealed class ForceUnlockConfiguration : IBindable
{
    /// <summary>
    /// The state store whose lock is forcibly released. force-unlock only touches the lock and never contacts the
    /// live database, so this is the sole input.
    /// </summary>
    public StateConfig? State { get; set; }

    /// <summary>
    /// Whether to skip the interactive confirmation prompt before forcibly releasing the lock.
    /// </summary>
    public bool Force { get; private set; }

    public void Bind(DdlProjectConfig project, ParseResult cli)
    {
        State = project.State;
        ForceUnlockOptions.Force.Bind(cli, f => Force = f);
    }
}
