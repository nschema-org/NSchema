using NSchema.State.Domain;

namespace NSchema.State;

/// <summary>
/// The outcome of a state read.
/// </summary>
/// <param name="State">The recorded state, or <see langword="null"/> when no snapshot has been recorded yet.</param>
public sealed record StateReadResult(DatabaseState? State);
