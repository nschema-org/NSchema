using System.CommandLine;
using NSchema.Configuration;
using NSchema.Configuration.Binding;
using NSchema.Configuration.State;

namespace NSchema.Commands.Lock.Status;

/// <summary>
/// Configuration for the <c>lock status</c> command.
/// </summary>
internal sealed class LockStatusConfiguration : IBindable
{
    /// <summary>
    /// The state store whose lock is inspected.
    /// </summary>
    public StateConfiguration? State { get; set; }

    /// <summary>
    /// Whether to return the detailed exit code (<c>2</c> when the state is locked).
    /// </summary>
    public bool DetailedExitCode { get; private set; }

    public void Bind(ProjectConfiguration project, ParseResult cli)
    {
        State = project.State;
        LockStatusOptions.DetailedExitCode.Bind(cli, d => DetailedExitCode = d);
    }
}
