using NSchema.Migration.Plan;

namespace NSchema.Migration;

/// <summary>
/// Builds a <see cref="MigrationPlan"/> by diffing the desired schema against the current database state.
/// </summary>
internal interface IMigrationPlanner
{
    /// <summary>
    /// Generates a migration plan that outlines the necessary steps to migrate a database schema from its current state to a target state.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The generated migration plan and any non-fatal diagnostics.</returns>
    Task<MigrationPlanResult> Plan(CancellationToken cancellationToken = default);
}
