namespace NSchema.Hosting;

/// <summary>
/// Captures the live current schema into the state store after an apply (or a refresh), so a later offline
/// plan can diff against it.
/// </summary>
internal interface IStateCapturer
{
    /// <summary>
    /// Reads the live current schema and writes it to the state store.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> if a snapshot was written; <see langword="false"/> if no state store is configured.</returns>
    Task<bool> Capture(CancellationToken cancellationToken = default);
}
