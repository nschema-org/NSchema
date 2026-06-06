using System.CommandLine;
using NSchema.Cli.Configuration;
using NSchema.Cli.Configuration.Provider;
using NSchema.Cli.Configuration.State;

namespace NSchema.Cli.Commands.Refresh;

/// <summary>
/// configuration for the refresh command.
/// </summary>
internal sealed class RefreshConfiguration : IConfigurable
{
    /// <summary>
    /// The database provider supplying the live schema.
    /// </summary>
    public ProviderConfig Provider { get; init; } = new();

    /// <summary>
    /// The state store the live schema is written to.
    /// </summary>
    public StateConfig State { get; init; } = new();

    public void Configure(ParseResult result)
    {
        Provider.Configure(result);
        State.Configure(result);
    }
}
