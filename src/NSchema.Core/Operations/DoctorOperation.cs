using NSchema.Deployment.Plugins;
using NSchema.Model;
using NSchema.Operations.Progress;
using NSchema.State;
using NSchema.State.Locks.Plugins;
using NSchema.State.Plugins;

namespace NSchema.Operations;

/// <summary>
/// Probes the configured infrastructure end to end.
/// </summary>
/// <remarks>
/// The building blocks are injected directly (rather than via <c>ICurrentDatabaseProvider</c>) so a missing source reads as "not configured" and the like.
/// </remarks>
internal sealed class DoctorOperation(
    IProgress<OperationProgress> progress,
    IDatabaseStateSerializer serializer,
    IDatabaseIntrospector? online = null,
    IDatabaseStateStore? store = null,
    IStateLock? stateLock = null
) : IOperation<DoctorArguments, Result<DoctorResult>>
{
    public async Task<Result<DoctorResult>> Execute(DoctorArguments arguments, CancellationToken cancellationToken = default)
    {
        var diagnostics = new DiagnosticCollection
        {
            await CheckDatabase(cancellationToken),
            await CheckStateStore(cancellationToken),
        };

        // The lock check only means anything when the configured backend actually provides a lock; otherwise there is
        // nothing to probe.
        if (stateLock is not null)
        {
            diagnostics.Add(await CheckStateLock(stateLock, cancellationToken));
        }

        return Result.Success(new DoctorResult(diagnostics));
    }

    private async Task<Diagnostic> CheckDatabase(CancellationToken cancellationToken)
    {
        if (online is null)
        {
            return DoctorDiagnostics.DatabaseNotConfigured;
        }

        progress.Report(OperationProgress.Step("Checking database connectivity..."));

        try
        {
            // A full introspection is the honest end-to-end probe: it exercises the same path plan/apply rely on.
            var schema = await online.GetDatabase(PlanningScope.All, cancellationToken);
            if (schema.IsFailure)
            {
                return DoctorDiagnostics.DatabaseUnreachable(Describe(schema));
            }

            return DoctorDiagnostics.DatabaseConnected(schema.Require().Schemas.Count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return DoctorDiagnostics.DatabaseUnreachable(ExceptionMessage.Describe(ex));
        }
    }

    private async Task<Diagnostic> CheckStateStore(CancellationToken cancellationToken)
    {
        if (store is null)
        {
            return DoctorDiagnostics.StateStoreNotConfigured;
        }

        progress.Report(OperationProgress.Step("Checking state store..."));
        ReadOnlyMemory<byte>? snapshot;
        try
        {
            var read = await store.Read(cancellationToken);
            if (read.IsFailure)
            {
                return DoctorDiagnostics.StateStoreUnreachable(Describe(read));
            }

            snapshot = read.Require().Payload;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return DoctorDiagnostics.StateStoreUnreachable(ExceptionMessage.Describe(ex));
        }

        // A missing or empty payload is a bootstrap store — reachable, with nothing recorded yet — not a corruption.
        if (snapshot is null or { IsEmpty: true })
        {
            return DoctorDiagnostics.StateStoreEmpty;
        }

        // Reachable is necessary but not sufficient — a payload we can't deserialize would break every plan, so prove
        // the recorded snapshot still round-trips through the serializer.
        try
        {
            serializer.Deserialize(snapshot.Value);
            return DoctorDiagnostics.StateStoreValid;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return DoctorDiagnostics.StateStoreUnreadable(ex.Message);
        }
    }

    private async Task<Diagnostic> CheckStateLock(IStateLock @lock, CancellationToken cancellationToken)
    {
        progress.Report(OperationProgress.Step("Checking state lock..."));
        try
        {
            var peeked = await @lock.Peek(cancellationToken);
            if (peeked.IsFailure)
            {
                return DoctorDiagnostics.StateLockUncheckable(Describe(peeked));
            }

            return peeked.Require().Held is not { } info
                ? DoctorDiagnostics.StateLockFree
                // A held lock is a state, not a misconfiguration — it may be a legitimately-running operation — so
                // report it for visibility (warning) without counting it as a failure.
                : DoctorDiagnostics.StateLockHeld(info);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return DoctorDiagnostics.StateLockUncheckable(ExceptionMessage.Describe(ex));
        }
    }

    // Folds a backend's reported errors into one line for the check's summary.
    private static string Describe(Result result) => string.Join("; ", result.Errors.Select(error => error.Message));
}
