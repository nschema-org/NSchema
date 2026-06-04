using NSchema.Cli.Configuration.Provider;
using NSchema.Cli.Configuration.State;

namespace NSchema.Cli.Commands.Refresh;

/// <summary>
/// configuration for the refresh command.
/// </summary>
internal sealed class RefreshConfiguration
{
    /// <summary>
    /// The database provider supplying the live schema.
    /// </summary>
    public required ProviderConfig Provider { get; init; }

    /// <summary>
    /// The state store the live schema is written to.
    /// </summary>
    public required StateConfig State { get; init; }
}
