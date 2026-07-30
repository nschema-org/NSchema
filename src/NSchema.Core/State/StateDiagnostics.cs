namespace NSchema.State;

/// <summary>
/// The diagnostics minted by the state manager.
/// </summary>
internal static class StateDiagnostics
{
    internal static readonly DiagnosticSource Source = "state";

    /// <summary>
    /// Reading or writing state without a configured store.
    /// </summary>
    public static Diagnostic NotConfigured => Diagnostic.Error(Source, "not-configured", "No state store is configured.");

    /// <summary>
    /// A store the backend could not read or write — typically unreachable.
    /// </summary>
    public static Diagnostic Unreachable(Exception exception) =>
        Diagnostic.Error(Source, "state-unreachable", $"Could not reach the state store: {ExceptionMessage.Describe(exception):text}");

    /// <summary>
    /// A stored payload that could not be deserialized.
    /// </summary>
    public static Diagnostic UnreadablePayload(Exception exception) =>
        Diagnostic.Error(Source, "unreadable-payload", exception.Message);

    /// <summary>
    /// A raw push whose payload does not deserialize; nothing was written.
    /// </summary>
    public static Diagnostic InvalidRawPayload(Exception exception) =>
        Diagnostic.Error(Source, "invalid-raw-payload", $"The payload is not a valid state snapshot and was not written. {exception.Message:text}");
}
