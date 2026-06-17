using System.CommandLine;
using NSchema.Configuration;
using NSchema.Configuration.Binding;
using NSchema.Configuration.Ddl;
using NSchema.Configuration.Provider;
using NSchema.Configuration.State;
using NSchema.Diff.Policies;

namespace NSchema.Commands.Apply;

/// <summary>
/// configuration for the apply command.
/// </summary>
internal sealed class ApplyConfiguration : IBindable
{
    /// <summary>
    /// The environment to apply changes to, if any.
    /// </summary>
    public string? Environment { get; set; }

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
    /// Whether to skip the interactive confirmation prompt and apply immediately.
    /// </summary>
    public bool AutoApprove { get; private set; }

    /// <summary>
    /// Optional path to a plan previously saved with <c>plan --out</c>.
    /// </summary>
    public string? PlanFile { get; private set; }

    public void Bind(DdlProjectConfig project, ParseResult cli)
    {
        Provider.Bind(project, cli);
        State.Bind(project, cli);
        CommonOptions.Environment.Bind(project, cli, e => Environment = e);
        ApplyOptions.Destructive.Bind(project, cli, p => DestructiveActionPolicy = p);
        ApplyOptions.Scope.Bind(project, cli, s => Scope = s);
        ApplyOptions.AutoApprove.Bind(project, cli, a => AutoApprove = a);
        ApplyOptions.PlanFile.Bind(project, cli, p => PlanFile = p);
    }
}
