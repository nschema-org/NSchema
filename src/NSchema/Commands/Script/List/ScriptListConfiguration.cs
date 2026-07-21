using System.CommandLine;
using NSchema.Configuration;
using NSchema.Configuration.Binding;
using NSchema.Configuration.State;

namespace NSchema.Commands.Script.List;

/// <summary>
/// Configuration for <c>script list</c>.
/// </summary>
internal sealed class ScriptListConfiguration : IBindable
{
    /// <summary>
    /// The state store the execution ledger is read from (the configured backend).
    /// </summary>
    public StateConfiguration? State { get; set; }

    public void Bind(ProjectConfiguration project, ParseResult cli)
    {
        State = project.State;
    }
}
