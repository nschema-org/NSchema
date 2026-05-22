using NSchema.Migration.Plan;

namespace NSchema.Policies;

/// <summary>
/// Defines an interface for validating a migration plan against a set of rules or policies.
/// </summary>
public interface IMigrationPolicy
{
    /// <summary>
    /// Validates the given migration plan against the rules defined by this policy and returns a collection of any errors found during validation.
    /// </summary>
    /// <param name="plan">The migration plan to validate against this policy.</param>
    /// <returns>The collection of errors found during validation. If the migration plan is valid according to this policy, the collection will be empty.</returns>
    IEnumerable<PolicyError> Validate(MigrationPlan plan);
}
