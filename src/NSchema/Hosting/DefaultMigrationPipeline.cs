using NSchema.Migration;
using NSchema.Policies;

namespace NSchema.Hosting;

/// <summary>
/// Default <see cref="IMigrationPipeline"/>.
/// </summary>
/// <param name="planner">Builds the migration plan.</param>
/// <param name="reporter">Presents user-facing migration progress and artifacts.</param>
/// <param name="compiler">Compiles the plan into an executable unit of work.</param>
/// <param name="stateCapturer">Captures the resulting schema into the state store after an apply.</param>
internal sealed class DefaultMigrationPipeline(
    IMigrationPlanner planner,
    IMigrationReporter reporter,
    IMigrationCompiler compiler,
    IStateCapturer stateCapturer
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

        // Capture the resulting schema so a later offline plan can diff against it. A no-op when no store
        // is configured; runs even for an empty diff to keep the state fresh.
        await stateCapturer.Capture(cancellationToken);
    }

    public async Task Refresh(CancellationToken cancellationToken = default)
    {
        if (!await stateCapturer.Capture(cancellationToken))
        {
            throw new InvalidOperationException("Refresh requires a state store. Register one via UseStateStore(...) or UseStateStoreFile(...).");
        }
    }

    /// <summary>
    /// Computes the plan, presents the diff, compiles it into an execution, and presents the preview.
    /// </summary>
    private async Task<ICompiledMigration> Prepare(CancellationToken cancellationToken)
    {
        reporter.Info("Computing migration plan...");
        var result = await planner.Plan(cancellationToken);

        reporter.ReportDiagnostics(result.Diagnostics);

        if (result.HasErrors)
        {
            throw new PolicyViolationException(result.Errors.ToList());
        }

        reporter.ReportPlan(result.Plan);

        reporter.Info("Compiling migration plan...");
        var execution = await compiler.Compile(result.Plan, cancellationToken);
        reporter.ReportPreview(execution.Preview);

        return execution;
    }
}
