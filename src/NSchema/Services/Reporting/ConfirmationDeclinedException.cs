namespace NSchema.Services.Reporting;

/// <summary>
/// Thrown when an operation that needs confirmation is not approved.
/// </summary>
internal sealed class ConfirmationDeclinedException(string message) : Exception(message);
