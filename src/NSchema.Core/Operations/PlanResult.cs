using NSchema.Plan.Domain;

namespace NSchema.Operations;

/// <summary>
/// The result of a plan (or teardown plan).
/// </summary>
/// <param name="Plan">The computed plan, carried even when a policy blocks it; <see langword="null"/> only when planning could not run at all.</param>
public sealed record PlanResult(MigrationPlan? Plan)
{
    /// <summary>
    /// Whether applying the plan would change anything.
    /// </summary>
    public bool HasChanges => Plan is { IsEmpty: false };
}
