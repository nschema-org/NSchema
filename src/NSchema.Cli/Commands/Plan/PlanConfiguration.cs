using NSchema.Cli.Configuration.Provider;
using NSchema.Cli.Configuration.Schema;
using NSchema.Cli.Configuration.State;
using NSchema.Migration;

namespace NSchema.Cli.Commands.Plan;

/// <summary>
/// configuration for the plan command.
/// </summary>
internal sealed class PlanConfiguration
{
    /// <summary>
    /// How the desired schema is located and read.
    /// </summary>
    public required SchemaConfig Schema { get; init; }

    /// <summary>
    /// The database provider supplying the live schema; offline when no section is populated.
    /// </summary>
    public required ProviderConfig Provider { get; init; }

    /// <summary>
    /// The state store enabling offline planning; absent when no section is populated.
    /// </summary>
    public required StateConfig State { get; init; }

    /// <summary>
    /// Optional scope filter limiting the plan to specific database schemas (namespaces).
    /// </summary>
    public string[]? Scope { get; init; }

    /// <summary>
    /// The policy applied when the plan contains destructive actions.
    /// </summary>
    public DestructiveActionPolicy? DestructiveActionPolicy { get; init; }
}
