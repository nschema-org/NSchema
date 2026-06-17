using System.CommandLine;
using NSchema.Configuration;
using NSchema.Configuration.Binding;
using NSchema.Configuration.Ddl;
using NSchema.Configuration.Provider;
using NSchema.Configuration.State;

namespace NSchema.Commands.Refresh;

/// <summary>
/// configuration for the refresh command.
/// </summary>
internal sealed class RefreshConfiguration : IBindable
{
    /// <summary>
    /// The database provider supplying the live schema.
    /// </summary>
    public ProviderConfig Provider { get; init; } = new();

    /// <summary>
    /// The state store the live schema is written to.
    /// </summary>
    public StateConfig State { get; init; } = new();

    /// <summary>
    /// The selected environment, if any.
    /// </summary>
    public string? Environment { get; private set; }

    public void Bind(DdlProjectConfig project, ParseResult cli)
    {
        Provider.Bind(project, cli);
        State.Bind(project, cli);
        CommonOptions.Environment.Bind(project, cli, e => Environment = e);
    }
}
