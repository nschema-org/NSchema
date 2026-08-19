using NSchema.State.Plugins;

namespace NSchema.State;

/// <summary>
/// The default <see cref="IDatabaseStateManager"/>.
/// </summary>
internal sealed class DatabaseStateManager(IDatabaseStateSerializer serializer, IDatabaseStateStore? store = null) : IDatabaseStateManager
{

    public bool IsConfigured => store is not null;

    public async Task<Result<StateReadResult>> Read(StateReadArguments arguments, CancellationToken cancellationToken = default)
    {
        if (store is null)
        {
            return NotConfigured<StateReadResult>();
        }

        var read = await store.Read(cancellationToken);
        if (read.IsFailure)
        {
            return Result.Failure<StateReadResult>(read.Diagnostics);
        }

        if (read.Require().Payload is not { } snapshot)
        {
            return new StateReadResult(null);
        }

        try
        {
            return new StateReadResult(serializer.Deserialize(snapshot));
        }
        catch (Exception ex) when (ex is StateDeserializationException or NotSupportedException)
        {
            return Result.Failure<StateReadResult>(StateDiagnostics.UnreadablePayload(ex));
        }
    }

    public async Task<Result<StateWriteResult>> Write(StateWriteArguments arguments, CancellationToken cancellationToken = default)
    {
        if (store is null)
        {
            return NotConfigured<StateWriteResult>();
        }

        var payload = serializer.Serialize(arguments.State);
        var written = await store.Write(payload, cancellationToken);

        return written.IsFailure
            ? Result.Failure<StateWriteResult>(written.Diagnostics)
            : new StateWriteResult(payload.Length);
    }

    public async Task<Result<StateRawReadResult>> ReadRaw(StateRawReadArguments arguments, CancellationToken cancellationToken = default)
    {
        if (store is null)
        {
            return NotConfigured<StateRawReadResult>();
        }

        var read = await store.Read(cancellationToken);

        return read.IsFailure
            ? Result.Failure<StateRawReadResult>(read.Diagnostics)
            : new StateRawReadResult(read.Require().Payload);
    }

    public async Task<Result<StateRawWriteResult>> WriteRaw(StateRawWriteArguments arguments, CancellationToken cancellationToken = default)
    {
        if (store is null)
        {
            return NotConfigured<StateRawWriteResult>();
        }

        try
        {
            serializer.Deserialize(arguments.Payload);
        }
        catch (Exception ex) when (ex is StateDeserializationException or NotSupportedException)
        {
            return Result.Failure<StateRawWriteResult>(StateDiagnostics.InvalidRawPayload(ex));
        }

        var written = await store.Write(arguments.Payload, cancellationToken);

        return written.IsFailure
            ? Result.Failure<StateRawWriteResult>(written.Diagnostics)
            : new StateRawWriteResult(arguments.Payload.Length);
    }

    private static Result<T> NotConfigured<T>() => Result.Failure<T>(StateDiagnostics.NotConfigured);
}
