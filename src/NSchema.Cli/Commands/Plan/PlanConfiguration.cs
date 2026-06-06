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
internal sealed class PlanConfiguration : IBindable
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

    public void Bind(ParseResult result)
    {
        CommonOptions.Scope.Bind(result, s => Scope = s);
        CommonOptions.Destructive.Bind(result, p => DestructiveActionPolicy = p);

        Schema.Bind(result);
        Provider.Bind(result);
        State.Bind(result);
    }
}
