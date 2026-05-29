using NSchema.Migration;
using NSchema.Policies;

namespace NSchema.Hosting;

/// <summary>
/// Default <see cref="IMigrationPipeline"/>.
/// </summary>
/// <param name="planner">Builds the migration plan.</param>
/// <param name="planRenderer">Renders the migration plan as a human-readable diff.</param>
/// <param name="reporter">The reporter for user-facing migration progress.</param>
/// <param name="compiler">Compiles the plan into an executable unit of work.</param>
internal sealed class DefaultMigrationPipeline(
    IMigrationPlanner planner,
    IMigrationPlanRenderer planRenderer,
    IMigrationReporter reporter,
    IMigrationCompiler compiler
) : IMigrationPipeline
{
    public async Task Plan(CancellationToken cancellationToken = default)
    {
        reporter.Info("Running in Plan mode. No changes will be applied to the database.");
        await Prepare(cancellationToken);
    }

    public async Task Apply(CancellationToken cancellationToken = default)
    {
        var execution = await Prepare(cancellationToken);

        try
        {
            reporter.Info("Running database migration...");
            await execution.Execute(cancellationToken);
            reporter.Info("Migration completed successfully.");
        }
        catch (Exception ex)
        {
            reporter.Error($"Migration failed: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Computes the plan, renders the diff, compiles it into an execution, and reports the preview.
    /// </summary>
    private async Task<ICompiledMigration> Prepare(CancellationToken cancellationToken)
    {
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

        reporter.Info("Compiling migration plan...");
        var execution = await compiler.Compile(plan, cancellationToken);
        foreach (var line in execution.Preview)
        {
            reporter.Info(line);
        }

        return execution;
    }
}
