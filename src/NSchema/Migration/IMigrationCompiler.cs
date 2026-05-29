using NSchema.Migration.Plan;

namespace NSchema.Migration;

/// <summary>
/// Compiles a migration plan into an executable unit of work for a target.
/// </summary>
public interface IMigrationCompiler
{
    /// <summary>
    /// Compiles the given migration plan into a unit that can be previewed and executed.
    /// </summary>
    /// <param name="plan">The migration plan to compile.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The compiled unit of work.</returns>
    Task<ICompiledMigration> Compile(MigrationPlan plan, CancellationToken cancellationToken = default);
}
