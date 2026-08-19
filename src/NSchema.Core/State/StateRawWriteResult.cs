namespace NSchema.State;

/// <summary>
/// The outcome of a raw state write.
/// </summary>
/// <param name="PayloadSize">The size of the written payload, in bytes.</param>
public sealed record StateRawWriteResult(int PayloadSize);
