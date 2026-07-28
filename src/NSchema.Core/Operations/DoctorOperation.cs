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
        const string source = "database";
        if (online is null)
        {
            return Diagnostic.Info(source, "Database: not configured (offline mode).");
        }

        progress.Report(OperationProgress.Step("Checking database connectivity..."));

        try
        {
            // A full introspection is the honest end-to-end probe: it exercises the same path plan/apply rely on.
            var schema = await online.GetDatabase(PlanningScope.All, cancellationToken);
            if (schema.IsFailure)
            {
                return Diagnostic.Error(source, $"Database: unreachable — {Describe(schema):text}");
            }

            return Diagnostic.Info(source, $"Database: connected ({StatusHelpers.Count(schema.Require().Schemas.Count, "schema")} visible).");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Diagnostic.Error(source, $"Database: unreachable — {ExceptionMessage.Describe(ex):text}");
        }
    }

    private async Task<Diagnostic> CheckStateStore(CancellationToken cancellationToken)
    {
        const string source = "state-store";
        if (store is null)
        {
            return Diagnostic.Info(source, "State store: not configured (offline planning unavailable).");
        }

        progress.Report(OperationProgress.Step("Checking state store..."));
        ReadOnlyMemory<byte>? snapshot;
        try
        {
            var read = await store.Read(cancellationToken);
            if (read.IsFailure)
            {
                return Diagnostic.Error(source, $"State store: unreachable — {Describe(read):text}");
            }

            snapshot = read.Require().Payload;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Diagnostic.Error(source, $"State store: unreachable — {ExceptionMessage.Describe(ex):text}");
        }

        // A missing or empty payload is a bootstrap store — reachable, with nothing recorded yet — not a corruption.
        if (snapshot is null or { IsEmpty: true })
        {
            return Diagnostic.Info(source, "State store: reachable (no state recorded yet).");
        }

        // Reachable is necessary but not sufficient — a payload we can't deserialize would break every plan, so prove
        // the recorded snapshot still round-trips through the serializer.
        try
        {
            serializer.Deserialize(snapshot.Value);
            return Diagnostic.Info(source, "State store: reachable, recorded state is valid.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Diagnostic.Error(source, $"State store: reachable but the recorded state is unreadable — {ex.Message:text}");
        }
    }

    private async Task<Diagnostic> CheckStateLock(IStateLock @lock, CancellationToken cancellationToken)
    {
        const string source = "state-lock";
        progress.Report(OperationProgress.Step("Checking state lock..."));
        try
        {
            var peeked = await @lock.Peek(cancellationToken);
            if (peeked.IsFailure)
            {
                return Diagnostic.Error(source, $"State lock: could not be checked — {Describe(peeked):text}");
            }

            return peeked.Require().Held is not { } info
                ? Diagnostic.Info(source, "State lock: free.")
                // A held lock is a state, not a misconfiguration — it may be a legitimately-running operation — so
                // report it for visibility (warning) without counting it as a failure.
                : Diagnostic.Warning(source, $"State lock: held by {info.Who} (operation '{info.Operation}', since {info.CreatedUtc:u}).");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Diagnostic.Error(source, $"State lock: could not be checked — {ExceptionMessage.Describe(ex):text}");
        }
    }

    // Folds a backend's reported errors into one line for the check's summary.
    private static string Describe(Result result) => string.Join("; ", result.Errors.Select(error => error.Message));
}
