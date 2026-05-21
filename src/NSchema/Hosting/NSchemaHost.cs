using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSchema.Migration;

namespace NSchema.Hosting;

/// <summary>
/// The hosted service that runs the pipeline.
/// </summary>
/// <param name="logger">The logger for the pipeline host.</param>
/// <param name="lifetime">The application lifetime.</param>
/// <param name="runner">The service that will be used to run the pipeline.</param>
/// <param name="migrator">The migrator, used to generate SQL in dry-run mode.</param>
/// <param name="options">The migration options.</param>
internal class NSchemaHost(
    ILogger<NSchemaHost> logger,
    IOptions<MigrationOptions> options,
    IHostApplicationLifetime lifetime,
    INSchemaRunner runner,
    ISchemaMigrator migrator
) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            bool isDryRun = options.Value.DryRun;

            if (isDryRun)
            {
                var schemaPlan = await runner.Plan(cancellationToken);
                var statementPlan = migrator.Plan(schemaPlan);

                if (statementPlan.IsEmpty)
                {
                    logger.LogInformation("Dry run: no changes detected.");
                }
                else
                {
                    logger.LogInformation("Dry run: {Count} statement(s) would be executed:\n\n{Script}",
                        statementPlan.Statements.Count,
                        string.Join(";\n\n", statementPlan) + ";");
                }
            }
            else
            {
                await runner.Apply(cancellationToken);
            }
        }
        finally
        {
            // Exit the application gracefully now that the pipeline has run.
            logger.LogDebug("Requesting application stop...");
            lifetime.StopApplication();
        }
    }
}
