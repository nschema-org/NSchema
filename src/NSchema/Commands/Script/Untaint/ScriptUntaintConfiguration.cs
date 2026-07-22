using System.CommandLine;
using NSchema.Configuration;
using NSchema.Configuration.Binding;
using NSchema.Configuration.State;

namespace NSchema.Commands.Script.Untaint;

/// <summary>
/// Configuration for <c>script untaint</c>.
/// </summary>
internal sealed class ScriptUntaintConfiguration : IBindable
{
    /// <summary>
    /// The state store holding the execution ledger (the configured backend).
    /// </summary>
    public StateConfiguration? State { get; set; }

    /// <summary>
    /// Whether to untaint without taking the state lock.
    /// </summary>
    public bool NoLock { get; private set; }

    public void Bind(ProjectConfiguration project, ParseResult cli)
    {
        State = project.State;
        ScriptUntaintOptions.NoLock.Bind(cli, n => NoLock = n);
    }
}
