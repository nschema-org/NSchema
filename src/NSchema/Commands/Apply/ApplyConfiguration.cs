using System.CommandLine;
using NSchema.Configuration;
using NSchema.Configuration.Binding;
using NSchema.Configuration.Provider;
using NSchema.Configuration.Schema;
using NSchema.Configuration.State;
using NSchema.Migration;

namespace NSchema.Commands.Apply;

/// <summary>
/// configuration for the apply command.
/// </summary>
internal sealed class ApplyConfiguration : IBindable
{
    /// <summary>
    /// How the desired schema is located and read.
    /// </summary>
    public SchemaConfig Schema { get; init; } = new();

    /// <summary>
    /// The database provider the plan is applied against.
    /// </summary>
    public ProviderConfig Provider { get; init; } = new();

    /// <summary>
    /// The state store the post-apply snapshot is written to; offline when no section is populated.
    /// </summary>
    public StateConfig State { get; init; } = new();

    /// <summary>
    /// Optional scope filter limiting the migration to specific database schemas (namespaces).
    /// </summary>
    public string[]? Scope { get; private set; }

    /// <summary>
    /// The policy applied when the plan contains destructive actions.
    /// </summary>
    public DestructiveActionPolicy? DestructiveActionPolicy { get; private set; }

    /// <summary>
    /// The policy applied when the plan contains destructive actions.
    /// </summary>
    public bool AutoApprove { get; private set; }

    public void Bind(ParseResult result)
    {
        CommonOptions.Scope.Bind(result, s => Scope = s);
        CommonOptions.Destructive.Bind(result, p => DestructiveActionPolicy = p);
        ApplyOptions.AutoApprove.Bind(result, a => AutoApprove = a);

        Schema.Bind(result);
        Provider.Bind(result);
        State.Bind(result);
    }
}
