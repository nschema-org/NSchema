using NSchema.State.Domain;

namespace NSchema.State;

/// <summary>
/// Serializes and deserializes <see cref="DatabaseState"/> snapshots for storage in a state backend.
/// </summary>
internal interface IDatabaseStateSerializer
{
    /// <summary>
    /// Serializes a state snapshot to a payload for storage.
    /// </summary>
    ReadOnlyMemory<byte> Serialize(DatabaseState state);

    /// <summary>
    /// Deserializes a state snapshot from a stored payload.
    /// </summary>
    /// <exception cref="StateDeserializationException">The payload is corrupt, truncated, or otherwise could not be deserialized.</exception>
    /// <exception cref="NotSupportedException">The payload was written by an incompatible newer format version.</exception>
    DatabaseState Deserialize(ReadOnlyMemory<byte> value);
}
