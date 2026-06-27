using System.CommandLine;
using NSchema.Configuration.Binding;
using NSchema.Configuration.Ddl;
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
    public StateConfig? State { get; set; }

    /// <summary>
    /// Optional scope filter limiting the output to specific database schemas (namespaces).
    /// </summary>
    public string[]? Scope { get; private set; }

    public void Bind(DdlProjectConfig project, ParseResult cli)
    {
        State = project.State;
        ShowOptions.Scope.Bind(cli, s => Scope = s);
    }
}
