using NSchema.Apply;
using NSchema.Operations.Progress;
using NSchema.Operations.Workflow;
using NSchema.Plan.Domain;
using NSchema.Plan.Policies;
using NSchema.State;

namespace NSchema.Operations;

/// <summary>
/// Applies a computed plan.
/// </summary>
internal sealed class ApplyOperation(
    IMigrationWorkflow workflow,
    IProgress<OperationProgress> progress,
    IEnumerable<IPlanPolicy> planPolicies,
    IDatabaseStateManager stateManager,
    ISqlExecutor? sqlExecutor = null
) : IOperation<ApplyArguments, Result<ApplyResult>>
{
    public async Task<Result<ApplyResult>> Execute(ApplyArguments args, CancellationToken cancellationToken = default)
    {
        // Applying records the run — the schema capture and the run-once ledger — so a run that cannot be
        // recorded is refused up front. A disposable database opts out explicitly with ephemeral state.
        if (!stateManager.IsConfigured)
        {
            return Result.Failure<ApplyResult>(ApplyDiagnostics.StoreRequired);
        }

        // Make sure policies are enforced at the point of execution.
        var findings = new DiagnosticCollection(planPolicies.SelectMany(p => p.Validate(args.Plan)));
        if (findings.HasErrors)
        {
            // Demote errors to warnings if the apply is forced.
            if (!args.Force)
            {
                return Result.Failure<ApplyResult>(findings.Append(ApplyDiagnostics.BlockedByPolicy));
            }
            findings.Demote(DiagnosticSeverity.Warning);
        }

        // An empty plan executes nothing, but still records state.
        if (args.Plan.IsEmpty)
        {
            var emptyCapture = await workflow.Refresh(args.Plan, force: true, cancellationToken);
            return Result.Success(new ApplyResult(args.Plan), findings.Concat(emptyCapture.Diagnostics));
        }

        if (sqlExecutor is null)
        {
            return Result.Failure<ApplyResult>(ApplyDiagnostics.MissingExecutor);
        }

        progress.Report(OperationProgress.Step("Applying the migration plan..."));
        progress.Report(OperationProgress.Detail(DescribeExecution(args.Plan)));

        try
        {
            await sqlExecutor.Execute(args.Plan.Statements, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The migration failed, possibly partway. Best-effort capture so the store reflects reality.
            // The plan is NOT passed as applied: we don't know which scripts ran.
            try
            {
                await workflow.Refresh(null, force: true, cancellationToken);
            }
            catch (Exception captureFailure) when (captureFailure is not OperationCanceledException)
            {
                // Swallow this so we don't mask the original error.
            }

            throw;
        }

        // Recording after execution must not refuse an unreadable payload — the SQL has already run, so the
        // store is force-updated to reflect reality and the reset ledger is surfaced as a warning instead.
        var capture = await workflow.Refresh(args.Plan, force: true, cancellationToken);

        return Result.Success(new ApplyResult(args.Plan), findings.Concat(capture.Diagnostics));
    }

    // A verbose progress line: the statement count and how many must run outside a transaction (e.g.
    // CREATE INDEX CONCURRENTLY) — the atomicity-breakers worth knowing about when diagnosing a partial apply.
    private static string DescribeExecution(MigrationPlan plan)
    {
        var count = plan.Statements.Count;
        var statements = count == 1 ? "1 statement" : $"{count} statements";
        var outside = plan.Statements.Count(s => s.RunOutsideTransaction);

        return outside == 0
            ? $"Executing {statements}."
            : $"Executing {statements} ({outside} must run outside a transaction).";
    }
}
