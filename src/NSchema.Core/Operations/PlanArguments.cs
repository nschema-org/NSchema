using NSchema.Model;

namespace NSchema.Operations;

/// <summary>
/// Arguments for computing a migration plan.
/// </summary>
public sealed record PlanArguments
{
    /// <summary>
    /// The schemas to scope the plan to. When unrestricted, scope is derived from the schemas under management.
    /// </summary>
    public PlanningScope Scope { get; init; } = PlanningScope.All;

    /// <summary>
    /// When set, the computed plan is written to this file path so it can be applied later.
    /// </summary>
    public string? OutFile { get; init; }

    /// <summary>
    /// The state to plan towards.
    /// </summary>
    public PlanTarget Target { get; init; } = PlanTarget.Project;
}
