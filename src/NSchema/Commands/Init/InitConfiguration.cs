using System.CommandLine;
using NSchema.Configuration.Binding;
using NSchema.Configuration.Ddl;

namespace NSchema.Commands.Init;

/// <summary>
/// Configuration for the init command.
/// </summary>
internal sealed class InitConfiguration : IBindable
{
    /// <summary>
    /// Whether to init the project even if the directory isn't empty.
    /// </summary>
    public bool Force { get; set; }

    public void Bind(DdlProjectConfig project, ParseResult cli)
    {
        InitOptions.Force.Bind(project, cli, f => Force = f);
    }
}
