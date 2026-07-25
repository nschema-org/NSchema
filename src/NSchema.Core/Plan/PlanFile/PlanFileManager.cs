using System.Text.Json;
using NSchema.Model.Serialization;

namespace NSchema.Plan.PlanFile;

/// <summary>
/// Reads and writes saved plan files.
/// </summary>
internal class PlanFileManager : IPlanFileManager
{
    public async Task<Result<PlanFileEnvelope>> Read(string path, CancellationToken cancellationToken)
    {
        byte[] bytes;
        try
        {
            bytes = await File.ReadAllBytesAsync(path, cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Result.Failure<PlanFileEnvelope>(PlanFileDiagnostics.UnreadableFile(path, ex));
        }

        try
        {
            return Deserialize(bytes);
        }
        catch (Exception ex) when (ex is PlanFileDeserializationException or NotSupportedException)
        {
            return Result.Failure<PlanFileEnvelope>(PlanFileDiagnostics.InvalidPayload(path, ex));
        }
    }

    public async Task Write(string path, PlanFileEnvelope envelope, CancellationToken cancellationToken)
    {
        var bytes = Serialize(envelope);
        await File.WriteAllBytesAsync(path, bytes.ToArray(), cancellationToken);
    }

    /// <summary>
    /// Serializes an envelope to bytes. Exposed (alongside <see cref="Deserialize"/>) for in-memory round-trip tests.
    /// </summary>
    public ReadOnlyMemory<byte> Serialize(PlanFileEnvelope envelope)
    {
        return JsonSerializer.SerializeToUtf8Bytes(envelope, ModelSerialization.Options);
    }

    /// <summary>
    /// Deserializes an envelope from bytes.
    /// </summary>
    /// <exception cref="PlanFileDeserializationException">The payload is corrupt, truncated, or could not be deserialized.</exception>
    /// <exception cref="NotSupportedException">The payload was written by an incompatible newer format version.</exception>
    public PlanFileEnvelope Deserialize(ReadOnlyMemory<byte> value)
    {
        PlanFileEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<PlanFileEnvelope>(value.Span, ModelSerialization.Options);
        }
        catch (Exception ex)
        {
            throw new PlanFileDeserializationException(
                "The plan file could not be deserialized; it may be corrupt, truncated, or written by an incompatible version of NSchema.",
                ex
            );
        }

        if (envelope is null)
        {
            throw new PlanFileDeserializationException("The plan file deserialized to null.");
        }

        if (envelope.Version > PlanFileEnvelope.CurrentVersion)
        {
            throw new NotSupportedException(
                $"Plan file version {envelope.Version} is newer than the supported version " +
                $"{PlanFileEnvelope.CurrentVersion}. Upgrade NSchema to read this plan file.");
        }

        return envelope;
    }
}
