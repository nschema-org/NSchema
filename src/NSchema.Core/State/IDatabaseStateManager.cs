namespace NSchema.State;

/// <summary>
/// The consumer-facing surface for reading and writing the recorded state.
/// </summary>
public interface IDatabaseStateManager
{
    /// <summary>
    /// Whether a state store is configured, so state can be read and written at all.
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Reads the recorded state. A missing snapshot is a success carrying a <see langword="null"/> state.
    /// </summary>
    /// <param name="arguments">The read arguments.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task<Result<StateReadResult>> Read(StateReadArguments arguments, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes the given state, replacing any recorded snapshot.
    /// </summary>
    /// <param name="arguments">The write arguments, carrying the state to persist.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task<Result<StateWriteResult>> Write(StateWriteArguments arguments, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the serialized state payload exactly as stored, without interpreting it.
    /// </summary>
    /// <param name="arguments">The read arguments.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task<Result<StateRawReadResult>> ReadRaw(StateRawReadArguments arguments, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that the given payload deserializes, then writes it verbatim.
    /// A payload that does not deserialize is a failure and nothing is written.
    /// </summary>
    /// <param name="arguments">The write arguments, carrying the payload to persist.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task<Result<StateRawWriteResult>> WriteRaw(StateRawWriteArguments arguments, CancellationToken cancellationToken = default);
}
