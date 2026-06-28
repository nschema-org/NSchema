namespace NSchema.Services.Confirmation;

/// <summary>
/// Thrown when an operation that needs confirmation is not approved.
/// exits 0.
/// </summary>
internal sealed class ConfirmationDeclinedException(string message) : Exception(message);
