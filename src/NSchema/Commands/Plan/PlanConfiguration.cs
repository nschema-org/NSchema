using System.CommandLine;
using NSchema.Configuration.Binding;
using NSchema.Configuration.Ddl;
using NSchema.Configuration.Plugins;
using NSchema.Configuration.State;
using NSchema.Diff.Policies;

namespace NSchema.Commands.Plan;

/// <summary>
/// configuration for the plan command.
/// </summary>
internal sealed class PlanConfiguration : IBindable
{
    /// <summary>
    /// The database provider supplying the live schema; offline when no section is populated.
    /// </summary>
    public PluginReference? Provider { get; set; }

    /// <summary>
    /// The state store enabling offline planning; absent when no section is populated.
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
    public DestructiveActionPolicy? DestructiveActionPolicy { get; private set; }

    /// <summary>
    /// Whether to preview a teardown of the managed schema (Terraform's <c>plan -destroy</c>) instead of a forward plan.
    /// </summary>
    // internal set: bound via Bind, but the mode toggle drives the validator's two branches, so tests set it directly.
    public bool Destroy { get; internal set; }

    /// <summary>
    /// Whether a state store is configured to read the managed schema from in <c>--destroy</c> mode; when absent,
    /// the teardown source falls back to the desired schema globbed from the working directory.
    /// </summary>
    public bool HasStateStore => State is not null;

    /// <summary>
    /// Optional path the computed plan is written to so it can be replayed later by <c>apply --plan-file</c>
    /// </summary>
    // internal set: bound via Bind, but paired with the Destroy toggle in tests, so they set it directly.
    public string? OutFile { get; internal set; }

    /// <summary>
    /// Whether to return the detailed exit code (<c>2</c> when the plan has changes).
    /// </summary>
    public bool DetailedExitCode { get; private set; }

    public void Bind(DdlProjectConfig project, ParseResult cli)
    {
        Provider = project.Provider;
        State = project.State;
        PlanOptions.Destructive.Bind(project, cli, p => DestructiveActionPolicy = p);
        PlanOptions.Scope.Bind(project, cli, s => Scope = s);
        PlanOptions.Destroy.Bind(project, cli, d => Destroy = d);
        PlanOptions.Out.Bind(project, cli, o => OutFile = o);
        PlanOptions.DetailedExitCode.Bind(project, cli, d => DetailedExitCode = d);
    }
}
