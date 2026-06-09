using System.CommandLine;
using NSchema.Configuration.Binding;
using NSchema.Configuration.Provider;
using NSchema.Configuration.State;

namespace NSchema.Commands.Destroy;

/// <summary>
/// Configuration for the destroy command.
/// </summary>
internal sealed class DestroyConfiguration : IBindable
{

    /// <summary>
    /// The database provider the teardown is generated and executed against.
    /// </summary>
    public ProviderConfig Provider { get; init; } = new();

    /// <summary>
    /// The state store the managed schema is read from and the post-destroy snapshot is written to; offline when no section is populated.
    /// </summary>
    public StateConfig State { get; init; } = new();

    /// <summary>
    /// Whether to skip the interactive confirmation prompt before tearing down the schema.
    /// </summary>
    public bool AutoApprove { get; private set; }

    /// <summary>
    /// Whether a state store is configured to read the managed schema from; when absent, the teardown source falls
    /// back to the desired schema globbed from the working directory.
    /// </summary>
    public bool HasStateStore => State.ConfiguredSectionCount >= 1;

    public void Bind(ParseResult result)
    {
        DestroyOptions.AutoApprove.Bind(result, a => AutoApprove = a);
        DestroyOptions.PostgresConnectionString.Bind(result, cs => Provider.EnsurePostgres().ConnectionString = cs);
    }
}
