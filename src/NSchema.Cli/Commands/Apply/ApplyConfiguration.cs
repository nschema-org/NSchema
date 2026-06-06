using NSchema.Cli.Configuration.Provider;
using NSchema.Cli.Configuration.Schema;
using NSchema.Cli.Configuration.State;
using NSchema.Migration;

namespace NSchema.Cli.Commands.Apply;

/// <summary>
/// configuration for the apply command.
/// </summary>
internal sealed class ApplyConfiguration
{
    /// <summary>
    /// How the desired schema is located and read.
    /// </summary>
    public required SchemaConfig Schema { get; init; }

    /// <summary>
    /// The database provider the plan is applied against.
    /// </summary>
    public required ProviderConfig Provider { get; init; }

    /// <summary>
    /// The state store the post-apply snapshot is written to; offline when no section is populated.
    /// </summary>
    public required StateConfig State { get; init; }

    /// <summary>
    /// Optional scope filter limiting the migration to specific database schemas (namespaces).
    /// </summary>
    public string[]? Scope { get; init; }

    /// <summary>
    /// The policy applied when the plan contains destructive actions.
    /// </summary>
    public DestructiveActionPolicy? DestructiveActionPolicy { get; init; }

    /// <summary>
    /// Whether to skip the confirmation prompt before applying.
    /// </summary>
    public bool AutoApprove { get; init; }
}
