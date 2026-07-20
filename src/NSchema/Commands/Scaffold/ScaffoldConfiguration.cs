using System.CommandLine;
using NSchema.Configuration;
using NSchema.Configuration.Binding;

namespace NSchema.Commands.Scaffold;

/// <summary>
/// Configuration for the scaffold command.
/// </summary>
internal sealed class ScaffoldConfiguration : IBindable
{
    /// <summary>
    /// Whether to scaffold the project even if the directory isn't empty.
    /// </summary>
    public bool Force { get; set; }

    /// <summary>
    /// The database provider to scaffold configuration and a sample schema for.
    /// </summary>
    public ProviderKind Provider { get; set; } = ProviderKind.Postgres;

    /// <summary>
    /// The state backend to scaffold configuration for.
    /// </summary>
    public BackendKind Backend { get; set; } = BackendKind.File;

    public void Bind(ProjectConfig project, ParseResult cli)
    {
        ScaffoldOptions.Force.Bind(cli, f => Force = f);
        ScaffoldOptions.Provider.Bind(cli, p => Provider = p);
        ScaffoldOptions.Backend.Bind(cli, b => Backend = b);
    }
}
