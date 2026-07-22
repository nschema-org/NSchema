using System.CommandLine;
using NSchema.Configuration;
using NSchema.Configuration.Binding;
using NSchema.Configuration.State;

namespace NSchema.Commands.State.Pull;

/// <summary>
/// Configuration for <c>state pull</c>.
/// </summary>
internal sealed class StatePullConfiguration : IBindable
{
    /// <summary>
    /// The state store the recorded state is pulled from (the configured backend).
    /// </summary>
    public StateConfiguration? State { get; set; }

    public void Bind(ProjectConfiguration project, ParseResult cli)
    {
        State = project.State;
    }
}
