using System.CommandLine;
using NSchema.Configuration;
using NSchema.Configuration.Binding;
using NSchema.Configuration.Plugins;
using NSchema.Configuration.State;

namespace NSchema.Commands.Apply;

/// <summary>
/// configuration for the apply command.
/// </summary>
internal sealed class ApplyConfiguration : IBindable
{
    /// <summary>
    /// The database provider the plan is applied against.
    /// </summary>
    public PluginReference? Database { get; set; }

    /// <summary>
    /// The state store the post-apply snapshot is written to; offline when no section is populated.
    /// </summary>
    public StateConfiguration? State { get; set; }

    /// <summary>
    /// Optional scope filter limiting the migration to specific database schemas (namespaces).
    /// </summary>
    public string[]? Scope { get; private set; }

    /// <summary>
    /// The policy applied when the plan contains destructive actions.
    /// </summary>
    public PolicyEnforcement? DestructiveActionPolicy { get; private set; }

    /// <summary>
    /// The policy applied when the plan contains changes that can fail on existing data.
    /// </summary>
    public PolicyEnforcement? DataHazardPolicy { get; private set; }

    /// <summary>
    /// Whether to skip the interactive confirmation prompt and apply immediately.
    /// </summary>
    public bool AutoApprove { get; private set; }

    /// <summary>
    /// Optional path to a plan previously saved with <c>plan --out</c>.
    /// </summary>
    public string? PlanFile { get; private set; }

    /// <summary>
    /// Whether to apply without acquiring the state lock (<c>--no-lock</c>).
    /// </summary>
    public bool NoLock { get; private set; }

    /// <summary>
    /// Whether to run against an in-memory state store instead of a configured <c>STATE</c> store.
    /// </summary>
    // internal set: bound via Bind, but the validator's presence rules branch on it, so tests set it directly.
    public bool Ephemeral { get; internal set; }

    public void Bind(ProjectConfiguration project, ParseResult cli)
    {
        Database = project.Database;
        State = project.State;
        ApplyOptions.Destructive.Bind(cli, p => DestructiveActionPolicy = p);
        ApplyOptions.DataHazards.Bind(cli, p => DataHazardPolicy = p);
        ApplyOptions.Scope.Bind(cli, s => Scope = s);
        ApplyOptions.AutoApprove.Bind(cli, a => AutoApprove = a);
        ApplyOptions.PlanFile.Bind(cli, p => PlanFile = p);
        ApplyOptions.NoLock.Bind(cli, n => NoLock = n);
        ApplyOptions.Ephemeral.Bind(cli, e => Ephemeral = e);
    }
}
