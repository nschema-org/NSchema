using NSchema.Migration;

namespace NSchema.Cli.Configuration;

/// <summary>
/// The project configuration for a CLI run.
/// </summary>
internal sealed class NSchemaConfiguration
{
    /// <summary>
    /// Whether to skip the confirmation prompt before applying a migration plan.
    /// </summary>
    public bool AutoApprove { get; set; }

    /// <summary>
    /// The database provider supplying the current (live) schema.
    /// </summary>
    public ProviderConfig Provider { get; set; } = new();

    /// <summary>
    /// How the desired schema is located and read. Required for the plan and apply commands.
    /// </summary>
    public SchemaConfig Schema { get; set; } = new();

    /// <summary>
    /// Optional scope filter limiting the migration to a specific set of database schemas (namespaces).
    /// </summary>
    public List<string> Scope { get; set; } = [];

    /// <summary>
    /// The policy applied when the plan contains destructive actions. Defaults to <see cref="DestructiveActionPolicy.Error"/>.
    /// </summary>
    public DestructiveActionPolicy? DestructiveActionPolicy { get; set; }

    /// <summary>
    /// The state store enabling offline planning and post-apply state capture.
    /// </summary>
    public StateConfig State { get; set; } = new();
}
