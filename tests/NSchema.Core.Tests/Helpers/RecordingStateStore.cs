using NSchema.State;
using NSchema.State.Backends;
using NSchema.State.Domain;

namespace NSchema.Tests.Helpers;

/// <summary>
/// In-memory <see cref="IDatabaseStateStore"/> that captures what was written. Used in place of the file store
/// when a test needs to assert on the persisted schema after the host has disposed its service provider.
/// </summary>
internal sealed class RecordingStateStore : IDatabaseStateStore
{
    private static readonly DatabaseStateSerializer _serializer = new();

    public DatabaseState? Written { get; private set; }

    // The explicit nullable default matters: a bare `null` here would convert through byte[] to an
    // empty (non-null) ReadOnlyMemory, which reads as a corrupt zero-byte payload rather than "no state".
    public Task<ReadOnlyMemory<byte>?> Read(CancellationToken cancellationToken = default) =>
        Task.FromResult(Written is null ? default(ReadOnlyMemory<byte>?) : _serializer.Serialize(Written));

    public Task Write(ReadOnlyMemory<byte> state, CancellationToken cancellationToken = default)
    {
        Written = _serializer.Deserialize(state);
        return Task.CompletedTask;
    }
}
