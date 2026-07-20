using System.CommandLine;
using NSchema.Configuration;
using NSchema.Configuration.Binding;
using NSchema.Configuration.Plugins;
using NSchema.Configuration.State;

namespace NSchema.Commands.Plan;

/// <summary>
/// configuration for the plan command.
/// </summary>
internal sealed class PlanConfiguration : IBindable
{
    /// <summary>
    /// The database provider rendering the plan's SQL; absent when no DATABASE statement is declared.
    /// </summary>
    public PluginReference? Provider { get; set; }

    /// <summary>
    /// The state store holding the recorded state the plan diffs against.
    /// </summary>
    public StateConfig? State { get; set; }

    /// <summary>
    /// Optional scope filter limiting the plan to specific database schemas (namespaces).
    /// </summary>
    public string[]? Scope { get; private set; }

    /// <summary>
    /// The policy applied when the plan contains destructive actions. Unused in <c>--destroy</c> mode, which bypasses
    /// the diff and its policies.
    /// </summary>
    public PolicyEnforcement? DestructiveActionPolicy { get; private set; }

    /// <summary>
    /// The policy applied when the plan contains changes that can fail on existing data. Unused in <c>--destroy</c>
    /// mode, which bypasses the diff and its policies.
    /// </summary>
    public PolicyEnforcement? DataHazardPolicy { get; private set; }

    /// <summary>
    /// Whether to preview a teardown of the managed schema (Terraform's <c>plan -destroy</c>) instead of a forward plan.
    /// </summary>
    // internal set: bound via Bind, but the mode toggle drives the validator's two branches, so tests set it directly.
    public bool Destroy { get; internal set; }

    /// <summary>
    /// Optional path the computed plan is written to so it can be replayed later by <c>apply --plan-file</c>
    /// </summary>
    // internal set: bound via Bind, but paired with the Destroy toggle in tests, so they set it directly.
    public string? OutFile { get; internal set; }

    /// <summary>
    /// Whether to return the detailed exit code (<c>2</c> when the plan has changes).
    /// </summary>
    public bool DetailedExitCode { get; private set; }

    /// <summary>
    /// Whether to run against an in-memory state store instead of a configured <c>STATE</c> store.
    /// </summary>
    // internal set: bound via Bind, but the validator's presence rules branch on it, so tests set it directly.
    public bool Ephemeral { get; internal set; }

    public void Bind(ProjectConfig project, ParseResult cli)
    {
        Provider = project.Provider;
        State = project.State;
        PlanOptions.Destructive.Bind(cli, p => DestructiveActionPolicy = p);
        PlanOptions.DataHazards.Bind(cli, p => DataHazardPolicy = p);
        PlanOptions.Scope.Bind(cli, s => Scope = s);
        PlanOptions.Destroy.Bind(cli, d => Destroy = d);
        PlanOptions.Out.Bind(cli, o => OutFile = o);
        PlanOptions.DetailedExitCode.Bind(cli, d => DetailedExitCode = d);
        PlanOptions.Ephemeral.Bind(cli, e => Ephemeral = e);
    }
}
