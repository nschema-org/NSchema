using System.CommandLine;
using NSchema.Configuration.Binding;
using NSchema.Configuration.Dsl;
using NSchema.Configuration.State;

namespace NSchema.Commands.Show;

/// <summary>
/// configuration for the show command.
/// </summary>
internal sealed class ShowConfiguration : IBindable
{
    /// <summary>
    /// The state store the recorded schema is read from. Show never contacts the live database.
    /// </summary>
    public StateConfig State { get; init; } = new();

    /// <summary>
    /// Optional scope filter limiting the output to specific database schemas (namespaces).
    /// </summary>
    public string[]? Scope { get; private set; }

    public void Bind(DslProjectConfig project, ParseResult cli)
    {
        ShowOptions.State.Bind(project, cli, s => State.CopyFrom(s));
        ShowOptions.Scope.Bind(project, cli, s => Scope = s);
    }
}
