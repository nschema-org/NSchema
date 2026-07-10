using System.CommandLine;
using NSchema.Configuration.Binding;
using NSchema.Configuration.Ddl;
using NSchema.Configuration.State;

namespace NSchema.Commands.Script.Taint;

/// <summary>
/// Configuration for <c>script taint</c>.
/// </summary>
internal sealed class ScriptTaintConfiguration : IBindable
{
    /// <summary>
    /// The state store holding the execution ledger (the configured backend).
    /// </summary>
    public StateConfig? State { get; set; }

    /// <summary>
    /// Whether to taint without taking the state lock.
    /// </summary>
    public bool NoLock { get; private set; }

    public void Bind(DdlProjectConfig project, ParseResult cli)
    {
        State = project.State;
        ScriptTaintOptions.NoLock.Bind(cli, n => NoLock = n);
    }
}
