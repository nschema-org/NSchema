using System.CommandLine;
using NSchema.Configuration.Binding;
using NSchema.Configuration;
using NSchema.Configuration.State;

namespace NSchema.Commands.State.Push;

/// <summary>
/// Configuration for <c>state push</c>.
/// </summary>
internal sealed class StatePushConfiguration : IBindable
{
    /// <summary>
    /// The state store the payload is pushed to (the configured backend).
    /// </summary>
    public StateConfig? State { get; set; }

    /// <summary>
    /// Whether to push without taking the state lock.
    /// </summary>
    public bool NoLock { get; private set; }

    public void Bind(ProjectConfig project, ParseResult cli)
    {
        State = project.State;
        StatePushOptions.NoLock.Bind(cli, n => NoLock = n);
    }
}
