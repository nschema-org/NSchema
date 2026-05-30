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
internal sealed class NSchemaHost(
    IOptions<MigrationOptions> options,
    IHostApplicationLifetime lifetime,
    IMigrationPipeline pipeline
) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            var operation = options.Value.Operation;
            var run = operation switch
            {
                MigrationOperation.Plan => pipeline.Plan(cancellationToken),
                MigrationOperation.Apply => pipeline.Apply(cancellationToken),
                MigrationOperation.Refresh => pipeline.Refresh(cancellationToken),
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
