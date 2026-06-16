using System.CommandLine;
using NSchema.Configuration.Binding;
using NSchema.Configuration.Dsl;
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

    public void Bind(DslProjectConfig project, ParseResult cli)
    {
        RefreshOptions.State.Bind(project, cli, s => State.CopyFrom(s));
        RefreshOptions.PostgresConnectionString.Bind(project, cli, cs => Provider.EnsurePostgres().ConnectionString = cs);
        RefreshOptions.CommandTimeout.Bind(project, cli, t => Provider.EnsurePostgres().CommandTimeout = t);
    }
}
