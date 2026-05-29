using Microsoft.Extensions.Options;
using NSchema.Migration;
using NSchema.Policies;

namespace NSchema.Hosting;

/// <summary>
/// Default <see cref="IMigrationPipeline"/>. Owns all user-facing reporting and the
/// plan-then-execute orchestration. The planner and executor remain free of reporter concerns.
/// </summary>
/// <param name="options">The migration options.</param>
/// <param name="reporter">The reporter for user-facing migration progress.</param>
/// <param name="planRenderer">Renders the migration plan as a human-readable diff.</param>
/// <param name="planner">Builds the migration plan.</param>
/// <param name="executor">Applies the plan to the target.</param>
internal sealed class DefaultMigrationPipeline(
    IOptions<MigrationOptions> options,
    IMigrationPlanner planner,
    IMigrationPlanRenderer planRenderer,
    IMigrationReporter reporter,
    IMigrationExecutor executor
) : IMigrationPipeline
{
    public async Task Run(CancellationToken cancellationToken = default)
    {
        var planOnly = options.Value.Operation == MigrationOperation.Plan;
        if (planOnly)
        {
            reporter.Info("Running in Plan mode. No changes will be applied to the database.");
        }

        reporter.Info("Computing migration plan...");
        Migration.Plan.MigrationPlan plan;
        try
        {
            plan = await planner.Plan(cancellationToken);
        }
        catch (PolicyViolationException ex)
        {
            reporter.Error("Validation failed:");
            foreach (var error in ex.Errors)
            {
                reporter.Error($"- {error.PolicyName}: {error.Message}");
            }
            throw;
        }

        reporter.Info(planRenderer.Render(plan) + Environment.NewLine);

        try
        {
            await executor.Apply(plan, planOnly, cancellationToken);
            if (!planOnly)
            {
                reporter.Info("Migration completed successfully.");
            }
        }
        catch (Exception ex)
        {
            reporter.Error($"Migration failed: {ex.Message}");
            throw;
        }
    }
}
