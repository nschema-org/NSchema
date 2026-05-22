using NSchema.Migration.Plan;

namespace NSchema.Migration;

/// <summary>
/// Defines a contract for transforming a migration plan, allowing for modifications or optimizations to be applied to the plan before execution.
/// </summary>
public interface IMigrationPlanTransformer
{
    /// <summary>
    /// Transforms the given migration plan, allowing for modifications or optimizations to be applied to the plan before execution.
    /// </summary>
    /// <param name="plan">The migration plan to be transformed.</param>
    /// <returns>The transformed migration plan.</returns>
    MigrationPlan Transform(MigrationPlan plan);
}
