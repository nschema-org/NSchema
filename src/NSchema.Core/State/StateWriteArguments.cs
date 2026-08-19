using NSchema.State.Domain;

namespace NSchema.State;

/// <summary>
/// Arguments for a state write.
/// </summary>
/// <param name="State">The state to persist, replacing any recorded snapshot.</param>
public sealed record StateWriteArguments(DatabaseState State);
