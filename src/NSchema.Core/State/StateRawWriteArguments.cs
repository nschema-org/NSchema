namespace NSchema.State;

/// <summary>
/// Arguments for a raw state write.
/// </summary>
/// <param name="Payload">The serialized state payload to persist verbatim, replacing any recorded snapshot.</param>
public sealed record StateRawWriteArguments(ReadOnlyMemory<byte> Payload);
