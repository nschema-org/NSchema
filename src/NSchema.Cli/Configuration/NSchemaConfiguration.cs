using System.Text.Json.Serialization;
using NSchema.Cli.Configuration.Import;
using NSchema.Cli.Configuration.Provider;
using NSchema.Cli.Configuration.Schema;
using NSchema.Cli.Configuration.State;
using NSchema.Migration;

namespace NSchema.Cli.Configuration;

/// <summary>
/// The project configuration for a CLI run.
/// </summary>
internal sealed class NSchemaConfiguration
{
    /// <summary>
    /// The database provider supplying the current (live) schema.
    /// </summary>
    public ProviderConfig Provider { get; init; } = new();

    /// <summary>
    /// The state store enabling offline planning and post-apply state capture.
    /// </summary>
    public StateConfig State { get; init; } = new();

    /// <summary>
    /// How the desired schema is located and read. Required for the plan and apply commands.
    /// </summary>
    public SchemaConfig Schema { get; init; } = new();

    /// <summary>
    /// Optional scope filter limiting the migration to a specific set of database schemas (namespaces).
    /// </summary>
    public string[]? Scope { get; set; }

    /// <summary>
    /// The policy applied when the plan contains destructive actions. Defaults to <see cref="DestructiveActionPolicy.Error"/>.
    /// </summary>
    public DestructiveActionPolicy? DestructiveActionPolicy { get; set; }

    /// <summary>
    /// Whether to skip the confirmation prompt before applying a migration plan.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool AutoApprove { get; set; }

    /// <summary>
    /// The import target used by the import command to write live schema as desired-schema source files.
    /// </summary>
    [JsonIgnore]
    public ImportTargetConfig ImportTarget { get; init; } = new();
}
