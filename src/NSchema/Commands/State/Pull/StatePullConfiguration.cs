using System.CommandLine;
using NSchema.Configuration.Binding;
using NSchema.Configuration.Ddl;
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
    public StateConfig? State { get; set; }

    public void Bind(DdlProjectConfig project, ParseResult cli)
    {
        State = project.State;
    }
}
