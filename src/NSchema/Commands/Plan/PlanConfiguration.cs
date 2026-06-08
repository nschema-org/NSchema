using System.CommandLine;
using NSchema.Configuration.Binding;
using NSchema.Configuration.Provider;
using NSchema.Configuration.Schema;
using NSchema.Configuration.State;
using NSchema.Migration;

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
    /// The policy applied when the plan contains destructive actions.
    /// </summary>
    public DestructiveActionPolicy? DestructiveActionPolicy { get; private set; }

    public void Bind(ParseResult result)
    {
        PlanOptions.Scope.Bind(result, s => Scope = s);
        PlanOptions.Destructive.Bind(result, p => DestructiveActionPolicy = p);

        PlanOptions.PostgresConnectionString.Bind(result, cs => Provider.EnsurePostgres().ConnectionString = cs);

        PlanOptions.SchemaFormat.Bind(result, f => Schema.Format = f);
        PlanOptions.SchemaDirectory.Bind(result, d => Schema.Directory = d);
        PlanOptions.SchemaPattern.Bind(result, p => Schema.Pattern = p);
    }
}
