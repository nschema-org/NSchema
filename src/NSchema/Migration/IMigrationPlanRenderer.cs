using NSchema.Migration.Plan;

namespace NSchema.Migration;

/// <summary>
/// Renders a <see cref="MigrationPlan"/> as a human-readable summary suitable for terminal output.
/// </summary>
public interface IMigrationPlanRenderer
{
    /// <summary>
    /// Produces a multi-line summary of the plan's actions.
    /// </summary>
    string Render(MigrationPlan plan);
}
