using System.Text.Json;
using NSchema.Model.Serialization;
using NSchema.State.Domain;

namespace NSchema.State;

/// <summary>
/// Serializes and deserializes <see cref="DatabaseState"/> snapshots to the versioned state envelope.
/// </summary>
internal sealed class DatabaseStateSerializer : IDatabaseStateSerializer
{
    /// <inheritdoc />
    public ReadOnlyMemory<byte> Serialize(DatabaseState state)
    {
        var envelope = new DatabaseStateEnvelope(DatabaseStateEnvelope.CurrentVersion, state.Database)
        {
            Scripts = state.Scripts,
            Managed = state.Managed,
            Declared = state.Declared,
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(envelope, ModelSerialization.Options);
        return bytes;
    }

    /// <inheritdoc />
    public DatabaseState Deserialize(ReadOnlyMemory<byte> state)
    {
        DatabaseStateEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<DatabaseStateEnvelope>(state.Span, ModelSerialization.Options);
        }
        catch (Exception ex)
        {
            throw new StateDeserializationException(
                "The stored state payload could not be deserialized; it may be corrupt, truncated, or written by an incompatible version of NSchema.",
                ex
            );
        }

        if (envelope is null)
        {
            throw new StateDeserializationException("State payload deserialized to null.");
        }

        if (envelope.Version > DatabaseStateEnvelope.CurrentVersion)
        {
            throw new NotSupportedException(
                $"State format version {envelope.Version} is newer than the supported version " +
                $"{DatabaseStateEnvelope.CurrentVersion}. Upgrade NSchema to read this state.");
        }

        return new DatabaseState(envelope.Database, envelope.Scripts, envelope.Managed, envelope.Declared);
    }
}
