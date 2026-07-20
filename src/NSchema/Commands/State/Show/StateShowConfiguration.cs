using System.CommandLine;
using NSchema.Configuration.Binding;
using NSchema.Configuration;
using NSchema.Configuration.State;

namespace NSchema.Commands.State.Show;

/// <summary>
/// Configuration for <c>state show</c> (without an explicit file: reads the configured store).
/// </summary>
internal sealed class StateShowConfiguration : IBindable
{
    /// <summary>
    /// The state store the recorded schema is read from (the configured backend).
    /// </summary>
    public StateConfig? State { get; set; }

    /// <summary>
    /// Optional scope filter limiting the output to specific database schemas (namespaces).
    /// </summary>
    public string[]? Scope { get; private set; }

    public void Bind(ProjectConfig project, ParseResult cli)
    {
        State = project.State;
        StateShowOptions.Scope.Bind(cli, s => Scope = s);
    }
}
