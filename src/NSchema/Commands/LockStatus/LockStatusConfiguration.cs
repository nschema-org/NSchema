using System.CommandLine;
using NSchema.Configuration.Binding;
using NSchema.Configuration.Ddl;
using NSchema.Configuration.State;

namespace NSchema.Commands.LockStatus;

/// <summary>
/// Configuration for the lock-status command.
/// </summary>
internal sealed class LockStatusConfiguration : IBindable
{
    /// <summary>
    /// The state store whose lock is inspected.
    /// </summary>
    public StateConfig State { get; init; } = new();

    /// <summary>
    /// Whether to return the detailed exit code (<c>2</c> when the state is locked).
    /// </summary>
    public bool DetailedExitCode { get; private set; }

    public void Bind(DdlProjectConfig project, ParseResult cli)
    {
        State.Bind(project, cli);
        LockStatusOptions.DetailedExitCode.Bind(project, cli, d => DetailedExitCode = d);
    }
}
