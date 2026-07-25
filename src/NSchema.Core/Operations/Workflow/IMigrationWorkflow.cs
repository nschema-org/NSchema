using NSchema.Model;
using NSchema.Plan.Domain;
using NSchema.Plan.Domain.Services;

namespace NSchema.Operations.Workflow;

/// <summary>
/// The imperative shell operations use to run the pure planner: it resolves schemas, invokes
/// <see cref="IMigrationPlanner"/>, surfaces diagnostics, and captures state to the store.
/// </summary>
internal interface IMigrationWorkflow
{
    /// <summary>
    /// Validates the project against the project policies.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task<Result> Validate(CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the recorded state and computes the plan migrating it to <paramref name="target"/>.
    /// </summary>
    /// <param name="target">The state to plan towards.</param>
    /// <param name="scope">The schemas under management this run.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task<Result<MigrationPlan>> ComputePlan(PlanTarget target, PlanningScope scope, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the state store from the live schema.
    /// </summary>
    /// <param name="applied">The plan that was just applied, or <see langword="null"/> when nothing ran.</param>
    /// <param name="force">When true, an existing payload that cannot be read is replaced (resetting the run-once
    /// ledger, flagged on the capture); when false, an unreadable payload fails the capture instead.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The capture outcome, or <see langword="null"/> when no state store is configured (nothing to capture).</returns>
    Task<Result<StateCapture>> Refresh(MigrationPlan? applied = null, bool force = false, CancellationToken cancellationToken = default);
}
