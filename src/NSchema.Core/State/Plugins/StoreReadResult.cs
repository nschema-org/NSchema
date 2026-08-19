namespace NSchema.State.Plugins;

/// <summary>
/// The outcome of reading the raw payload from a state store.
/// </summary>
/// <param name="Payload">The recorded payload, or <see langword="null"/> when nothing has been recorded yet.</param>
public sealed record StoreReadResult(ReadOnlyMemory<byte>? Payload);
