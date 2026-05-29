using NSchema.Migration.Plan;

namespace NSchema.Migration;

/// <summary>
/// Applies a migration plan to a target.
/// </summary>
public interface IMigrationExecutor
{
    /// <summary>
    /// Executes the given migration plan against the target.
    /// </summary>
    /// <param name="plan">The migration plan to apply.</param>
    /// <param name="planOnly">When true, the executor should preview what it would do (e.g. report the SQL it would run, or the file it would write) without making any changes.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task Apply(MigrationPlan plan, bool planOnly, CancellationToken cancellationToken = default);
}
