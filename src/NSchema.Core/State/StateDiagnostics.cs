namespace NSchema.State;

/// <summary>
/// The diagnostics minted by the state manager.
/// </summary>
internal static class StateDiagnostics
{
    private const string Source = "state";

    /// <summary>
    /// Reading or writing state without a configured store.
    /// </summary>
    public static Diagnostic NotConfigured => Diagnostic.Error(Source, "No state store is configured.");

    /// <summary>
    /// A stored payload that could not be deserialized.
    /// </summary>
    public static Diagnostic UnreadablePayload(Exception exception) =>
        Diagnostic.Error(Source, exception.Message);

    /// <summary>
    /// A raw push whose payload does not deserialize; nothing was written.
    /// </summary>
    public static Diagnostic InvalidRawPayload(Exception exception) =>
        Diagnostic.Error(Source, $"The payload is not a valid state snapshot and was not written. {exception.Message:text}");
}
