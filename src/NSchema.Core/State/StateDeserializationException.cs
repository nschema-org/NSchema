namespace NSchema.State;

/// <summary>
/// Thrown when a stored state payload cannot be deserialized into a schema snapshot.
/// </summary>
/// <param name="message">A description of the failure.</param>
/// <param name="innerException">The underlying failure that caused deserialization to fail, if any.</param>
internal sealed class StateDeserializationException(string message, Exception? innerException = null)
    : Exception(message, innerException)
{ }
