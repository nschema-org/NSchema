using NSchema.Plan.Domain;

namespace NSchema.Plan.Policies;

/// <summary>
/// Validates the complete <see cref="MigrationPlan"/> — the diff, its scripts, and the SQL statements that
/// execute it — against a set of rules or policies. Plan policies run after rendering, so they see exactly
/// what an apply would execute, and apply re-runs them against the plan it is given.
/// </summary>
public interface IPlanPolicy
{
    /// <summary>
    /// Validates the given plan against the rules defined by this policy and returns a collection of any
    /// findings. If the plan is valid according to this policy, the collection is empty.
    /// </summary>
    /// <param name="plan">The complete migration plan to validate against this policy.</param>
    IEnumerable<Diagnostic> Validate(MigrationPlan plan);
}
