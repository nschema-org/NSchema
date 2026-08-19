namespace NSchema.Plan.PlanFile;

/// <summary>
/// Thrown when a saved plan file cannot be deserialized into a <see cref="PlanFileEnvelope"/>.
/// </summary>
/// <param name="message">A description of the failure.</param>
/// <param name="innerException">The underlying failure that caused deserialization to fail, if any.</param>
internal sealed class PlanFileDeserializationException(string message, Exception? innerException = null) : Exception(message, innerException)
{
}
