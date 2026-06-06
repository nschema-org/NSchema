using System.CommandLine;
using NSchema.Cli.Configuration.Binding;
using NSchema.Cli.Configuration.Provider;
using NSchema.Cli.Configuration.State;

namespace NSchema.Cli.Commands.Refresh;

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

    public void Bind(ParseResult result)
    {
        Provider.Bind(result);
        State.Bind(result);
    }
}
