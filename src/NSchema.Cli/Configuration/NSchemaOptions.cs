using System.CommandLine;
using NSchema.Migration;

namespace NSchema.Cli.Configuration;

/// <summary>
/// The project configuration for a CLI run.
/// </summary>
internal sealed class NSchemaOptions
{
    /// <summary>
    /// Whether to skip the confirmation prompt before applying a migration plan.
    /// </summary>
    public bool AutoApprove { get; set; }

    /// <summary>
    /// The name of the bundled database provider to use (e.g. <c>postgres</c>). Selects the current-state source.
    /// </summary>
    public string? Provider { get; set; }

    /// <summary>
    /// The connection string the selected database provider connects with.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Paths or glob patterns identifying the desired-schema files (currently JSON).
    /// </summary>
    public List<string> Schemas { get; set; } = [];

    /// <summary>
    /// Optional scope filter limiting the migration to a specific set of schema names.
    /// </summary>
    public List<string> SchemaNames { get; set; } = [];

    /// <summary>
    /// The policy applied when the plan contains destructive actions. Defaults to <see cref="DestructiveActionPolicy.Error"/>.
    /// </summary>
    public DestructiveActionPolicy? DestructiveActionPolicy { get; set; }

    /// <summary>
    /// Optional state-store configuration enabling offline planning and post-apply state capture.
    /// </summary>
    public StateConfig? State { get; set; }

    /// <summary>
    /// State-store configuration.
    /// </summary>
    internal sealed class StateConfig
    {
        /// <summary>Path to the local file the schema state is persisted to and read from.</summary>
        public string? File { get; set; }
    }
}
