using System.CommandLine;
using NSchema.Configuration.Binding;
using NSchema.Configuration.Ddl;
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
    public StateConfig? State { get; set; }

    public void Bind(DdlProjectConfig project, ParseResult cli)
    {
        State = project.State;
    }
}
