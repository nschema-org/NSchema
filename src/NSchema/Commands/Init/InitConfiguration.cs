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

    /// <summary>
    /// The database provider to scaffold configuration and a sample schema for.
    /// </summary>
    public ProviderKind Provider { get; set; } = ProviderKind.Postgres;

    /// <summary>
    /// The state backend to scaffold configuration for.
    /// </summary>
    public BackendKind Backend { get; set; } = BackendKind.File;

    public void Bind(DdlProjectConfig project, ParseResult cli)
    {
        InitOptions.Force.Bind(cli, f => Force = f);
        InitOptions.Provider.Bind(cli, p => Provider = p);
        InitOptions.Backend.Bind(cli, b => Backend = b);
    }
}
