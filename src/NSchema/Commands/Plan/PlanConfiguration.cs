using System.CommandLine;
using NSchema.Configuration;
using NSchema.Configuration.Binding;
using NSchema.Configuration.Ddl;
using NSchema.Configuration.Provider;
using NSchema.Configuration.State;
using NSchema.Diff.Policies;

namespace NSchema.Commands.Plan;

/// <summary>
/// configuration for the plan command.
/// </summary>
internal sealed class PlanConfiguration : IBindable
{
    /// <summary>
    /// The environment to plan against, if any.
    /// </summary>
    public string? Environment { get; set; }

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
    /// Whether a state store is configured to read the managed schema from in <c>--destroy</c> mode; when absent,
    /// the teardown source falls back to the desired schema globbed from the working directory.
    /// </summary>
    public bool HasStateStore => State.ConfiguredSectionCount >= 1;

    /// <summary>
    /// Optional path the computed plan is written to so it can be replayed later by <c>apply --plan-file</c>
    /// </summary>
    // internal set: bound via Bind, but paired with the Destroy toggle in tests, so they set it directly.
    public string? OutFile { get; internal set; }

    public void Bind(DdlProjectConfig project, ParseResult cli)
    {
        Provider.Bind(project, cli);
        State.Bind(project, cli);
        CommonOptions.Environment.Bind(project, cli, e => Environment = e);
        PlanOptions.Destructive.Bind(project, cli, p => DestructiveActionPolicy = p);
        PlanOptions.Scope.Bind(project, cli, s => Scope = s);
        PlanOptions.Destroy.Bind(project, cli, d => Destroy = d);
        PlanOptions.Out.Bind(project, cli, o => OutFile = o);
    }
}
