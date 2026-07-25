using NSchema.Deployment;
using NSchema.Diff.Domain.Services;
using NSchema.Operations.Progress;
using NSchema.State;
using NSchema.State.Domain;

namespace NSchema.Operations;

/// <summary>
/// Compares the recorded state against the live database and reports how the live database has drifted from it.
/// </summary>
internal sealed class DriftOperation(IDatabaseProvider provider, IDatabaseStateManager stateManager, IDatabaseComparer comparer, IProgress<OperationProgress> progress)
    : IOperation<DriftArguments, Result<DriftResult>>
{
    public async Task<Result<DriftResult>> Execute(DriftArguments args, CancellationToken cancellationToken = default)
    {
        var diagnostics = new DiagnosticCollector();

        progress.Report(OperationProgress.Step("Reading recorded state..."));
        if (!diagnostics.TryTake(await stateManager.Read(new StateReadArguments(), cancellationToken), out var recorded))
        {
            return diagnostics.ToResult<DriftResult>(null);
        }
        // Before anything is recorded, drift measures against nothing.
        var recordedSchema = (recorded.State ?? DatabaseState.Empty).Database.ScopedTo(args.Scope);

        progress.Report(OperationProgress.Step("Reading live database..."));
        if (!diagnostics.TryTake(await provider.GetDatabase(args.Scope, cancellationToken), out var liveSchema))
        {
            return diagnostics.ToResult<DriftResult>(null);
        }

        // Diff direction: recorded -> live, so the changes describe how the live database has drifted from what we
        // recorded (an added object appears as Add, an out-of-band drop as Remove).
        var diff = comparer.Compare(AlignedDatabase.Unaligned(recordedSchema), liveSchema);
        return diagnostics.ToResult(new DriftResult(diff));
    }
}
