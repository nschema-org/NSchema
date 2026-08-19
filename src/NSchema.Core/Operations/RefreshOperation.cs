using NSchema.Operations.Workflow;
namespace NSchema.Operations;

/// <summary>
/// Reads the live current schema and writes it to the state store, under the state lock.
/// </summary>
internal sealed class RefreshOperation(IMigrationWorkflow workflow) : IOperation<RefreshArguments, Result<RefreshResult>>
{
    public async Task<Result<RefreshResult>> Execute(RefreshArguments args, CancellationToken cancellationToken = default)
    {
        var diagnostics = new DiagnosticCollector();
        if (!diagnostics.TryTake(await workflow.Refresh(null, args.Force, cancellationToken), out var capture))
        {
            return diagnostics.ToResult<RefreshResult>(null);
        }

        return diagnostics.ToResult(new RefreshResult(capture.Schema, capture.SnapshotBytes));
    }
}
