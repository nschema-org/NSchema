using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSchema.Migration;

namespace NSchema.Hosting;

/// <summary>
/// The hosted service that runs the database migration.
/// </summary>
/// <param name="options">The migration options.</param>
/// <param name="reporter">The reporter for user-facing migration progress.</param>
/// <param name="planRenderer">Renders the migration plan as a human-readable diff.</param>
/// <param name="lifetime">The application lifetime.</param>
/// <param name="migrator">The schema migrator.</param>
/// <param name="executor">The migration executor that applies the plan to the target.</param>
internal class NSchemaHost(
    IOptions<MigrationOptions> options,
    IMigrationReporter reporter,
    IMigrationPlanRenderer planRenderer,
    IHostApplicationLifetime lifetime,
    IMigrationPlanProvider migrator,
    IMigrationExecutor executor
) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (options.Value.DryRun)
            {
                reporter.Info("Dry run enabled. No changes will be applied to the database.");
            }

            reporter.Info("Computing migration plan...");
            var plan = await migrator.Plan(cancellationToken);

            reporter.Info(planRenderer.Render(plan) + Environment.NewLine);

            try
            {
                await executor.Apply(plan, options.Value.DryRun, cancellationToken);
                if (!options.Value.DryRun)
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
        finally
        {
            // Exit the application gracefully now that the pipeline has run.
            lifetime.StopApplication();
        }
    }
}
