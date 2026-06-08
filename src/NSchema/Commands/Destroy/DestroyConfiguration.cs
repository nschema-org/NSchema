using System.CommandLine;
using NSchema.Configuration;
using NSchema.Configuration.Binding;
using NSchema.Configuration.Provider;
using NSchema.Configuration.Schema;
using NSchema.Configuration.State;

namespace NSchema.Commands.Destroy;

/// <summary>
/// Configuration for the destroy command.
/// </summary>
internal sealed class DestroyConfiguration : IBindable
{
    /// <summary>
    /// How the desired schema is located and read; used as the managed-schema source when no state store is configured.
    /// </summary>
    public SchemaConfig Schema { get; init; } = new();

    /// <summary>
    /// The database provider the teardown is generated and executed against.
    /// </summary>
    public ProviderConfig Provider { get; init; } = new();

    /// <summary>
    /// The state store the managed schema is read from and the post-destroy snapshot is written to; offline when no section is populated.
    /// </summary>
    public StateConfig State { get; init; } = new();

    /// <summary>
    /// Optional scope filter limiting the teardown to specific database schemas (namespaces).
    /// </summary>
    public string[]? Scope { get; private set; }

    /// <summary>
    /// Whether to skip the interactive confirmation prompt before tearing down the schema.
    /// </summary>
    public bool AutoApprove { get; private set; }

    /// <summary>
    /// Whether a desired schema source is configured to fall back on when no state store is present.
    /// </summary>
    public bool HasSchema => !string.IsNullOrWhiteSpace(Schema.Directory);

    public void Bind(ParseResult result)
    {
        CommonOptions.Scope.Bind(result, s => Scope = s);
        CommonOptions.AutoApprove.Bind(result, a => AutoApprove = a);

        Schema.Bind(result);
        Provider.Bind(result);
        State.Bind(result);
    }
}
