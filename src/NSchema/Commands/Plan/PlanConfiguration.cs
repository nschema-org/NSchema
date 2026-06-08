using System.CommandLine;
using NSchema.Configuration.Binding;
using NSchema.Configuration.Provider;
using NSchema.Configuration.Schema;
using NSchema.Configuration.State;
using NSchema.Diff.Policies;

namespace NSchema.Commands.Plan;

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
    /// The policy applied when the plan contains destructive actions. Unused in <c>--destroy</c> mode, which bypasses
    /// the diff and its policies.
    /// </summary>
    public DestructiveActionPolicy? DestructiveActionPolicy { get; private set; }

    /// <summary>
    /// Whether to preview a teardown of the managed schema (Terraform's <c>plan -destroy</c>) instead of a forward plan.
    /// </summary>
    // internal set: bound via Bind, but the mode toggle drives the validator's two branches, so tests set it directly.
    public bool Destroy { get; internal set; }

    /// <summary>
    /// Whether a desired schema source is configured to fall back on when no state store is present (the teardown
    /// source in <c>--destroy</c> mode).
    /// </summary>
    public bool HasSchema => !string.IsNullOrWhiteSpace(Schema.Directory);

    public void Bind(ParseResult result)
    {
        PlanOptions.Scope.Bind(result, s => Scope = s);
        PlanOptions.Destructive.Bind(result, p => DestructiveActionPolicy = p);
        PlanOptions.Destroy.Bind(result, d => Destroy = d);
        PlanOptions.PostgresConnectionString.Bind(result, cs => Provider.EnsurePostgres().ConnectionString = cs);
    }
}
