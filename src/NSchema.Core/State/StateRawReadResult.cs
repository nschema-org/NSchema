namespace NSchema.State;

/// <summary>
/// The outcome of a raw state read.
/// </summary>
/// <param name="Payload">The serialized state payload exactly as stored, or <see langword="null"/> when no snapshot has been recorded yet.</param>
public sealed record StateRawReadResult(ReadOnlyMemory<byte>? Payload);
