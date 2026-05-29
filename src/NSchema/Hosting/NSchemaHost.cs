using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSchema.Migration;

namespace NSchema.Hosting;

/// <summary>
/// The hosted service that runs the migration pipeline once on startup and then stops the application.
/// </summary>
/// <param name="lifetime">The application lifetime.</param>
/// <param name="pipeline">The migration pipeline to run.</param>
/// <param name="options">The migration options, which select the operation to run.</param>
/// <param name="runContext">Carries an optional per-run operation override that takes precedence over the configured operation.</param>
internal sealed class NSchemaHost(
    IOptions<MigrationOptions> options,
    IHostApplicationLifetime lifetime,
    IMigrationPipeline pipeline,
    MigrationRunContext runContext
) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            var operation = runContext.Override ?? options.Value.Operation;
            var run = operation switch
            {
                MigrationOperation.Plan => pipeline.Plan(cancellationToken),
                MigrationOperation.Apply => pipeline.Apply(cancellationToken),
                _ => throw new ArgumentOutOfRangeException(nameof(options), operation, "Unknown migration operation."),
            };
            await run;
        }
        finally
        {
            lifetime.StopApplication();
        }
    }
}
