using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSchema.Migration;

namespace NSchema.Hosting;

/// <summary>
/// The hosted service that runs the database migration.
/// </summary>
/// <param name="reporter">The reporter for user-facing migration progress.</param>
/// <param name="planRenderer">Renders the migration plan as a human-readable diff.</param>
/// <param name="lifetime">The application lifetime.</param>
/// <param name="migrator">The schema migrator.</param>
/// <param name="sqlPlanner">The SQL planner, used to generate the SQL that will be executed.</param>
/// <param name="sqlExecutor">The SQL executor, used to run the generated SQL against the database.</param>
/// <param name="options">The migration options.</param>
internal class NSchemaHost(
    IOptions<MigrationOptions> options,
    IMigrationReporter reporter,
    IMigrationPlanRenderer planRenderer,
    IHostApplicationLifetime lifetime,
    IMigrationPlanProvider migrator,
    ISqlPlanner sqlPlanner,
    ISqlExecutor sqlExecutor
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
            var schemaPlan = await migrator.ComputeMigrationPlan(cancellationToken);

            reporter.Info(planRenderer.Render(schemaPlan) + Environment.NewLine);

            reporter.Info("Generating SQL statements...");
            var sqlPlan = sqlPlanner.Plan(schemaPlan);
            foreach (var statement in sqlPlan.Statements)
            {
                reporter.Info(statement.Sql);
            }

            if (!options.Value.DryRun)
            {
                try
                {
                    reporter.Info("Running database migration...");
                    await sqlExecutor.Execute(sqlPlan, cancellationToken);
                    reporter.Info("Migration completed successfully.");
                }
                catch (Exception ex)
                {
                    reporter.Error($"Migration failed: {ex.Message}");
                    throw;
                }
            }
        }
        finally
        {
            // Exit the application gracefully now that the pipeline has run.
            lifetime.StopApplication();
        }
    }
}
