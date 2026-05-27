using NSchema.Migration.Plan;

namespace NSchema.Migration;

/// <summary>
/// Defines an interface for providing a migration plan that describes the necessary steps to migrate a database schema from its current state to a desired target state.
/// </summary>
internal interface IMigrationPlanProvider
{
    /// <summary>
    /// Generates a migration plan that outlines the necessary steps to migrate a database schema from its current state to a target state.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests. If the operation is canceled, the method should stop processing and return as soon as possible.</param>
    /// <returns>The generated migration plan.</returns>
    Task<MigrationPlan> Plan(CancellationToken cancellationToken = default);
}
