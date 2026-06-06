using System.CommandLine;
using NSchema.Cli.Configuration;
using NSchema.Cli.Configuration.Provider;
using NSchema.Cli.Configuration.Schema;
using NSchema.Cli.Configuration.State;
using NSchema.Migration;

namespace NSchema.Cli.Commands.Plan;

/// <summary>
/// configuration for the plan command.
/// </summary>
internal sealed class PlanConfiguration : IConfigurable
{
    /// <summary>
    /// How the desired schema is located and read.
    /// </summary>
    public SchemaConfig Schema { get; init; } = new();

    /// <summary>
    /// The database provider supplying the live schema; offline when no section is populated.
    /// </summary>
    public ProviderConfig Provider { get; init; } = new();

    /// <summary>
    /// The state store enabling offline planning; absent when no section is populated.
    /// </summary>
    public StateConfig State { get; init; } = new();

    /// <summary>
    /// Optional scope filter limiting the plan to specific database schemas (namespaces).
    /// </summary>
    public string[]? Scope { get; private set; }

    /// <summary>
    /// The policy applied when the plan contains destructive actions.
    /// </summary>
    public DestructiveActionPolicy? DestructiveActionPolicy { get; private set; }

    public void Configure(ParseResult result)
    {
        if (CommonOptions.Scope.TryResolve(result, out var scope))
        {
            Scope = scope;
        }

        if (CommonOptions.Destructive.TryResolve(result, out var policy))
        {
            DestructiveActionPolicy = policy;
        }

        Schema.Configure(result);
        Provider.Configure(result);
        State.Configure(result);
    }
}
