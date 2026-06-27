using NSchema.Operations;
using NSchema.State.Model;

namespace NSchema.Services;

/// <summary>
/// Writes consistently-formatted messages to the console.
/// </summary>
internal interface IConsoleMessenger
{
    /// <summary>
    /// Reports a status / progress message.
    /// </summary>
    void Report(MessageKind kind, string message);

    /// <summary>
    /// Announces a neutral, top-level message.
    /// </summary>
    void Announce(ConsoleMessage message);

    /// <summary>
    /// Reports progress narration.
    /// </summary>
    void Progress(ConsoleMessage message);

    /// <summary>
    /// Reports a success outcome.
    /// </summary>
    void Success(ConsoleMessage message);

    /// <summary>
    /// Reports a warning.
    /// </summary>
    void Warn(ConsoleMessage message);

    /// <summary>
    /// Writes an indented secondary line beneath a headline (e.g. the lock id and expiry under a <c>lock status</c> line).
    /// </summary>
    void Detail(string message);

    /// <summary>
    /// An indented secondary line.
    /// </summary>
    void Detail(ConsoleMessage message);

    /// <summary>
    /// Prints which environment a run is targeting.
    /// </summary>
    void ReportEnvironment(string? environment);

    /// <summary>
    /// Reports the information about a lock.
    /// </summary>
    void ReportLockInfo(StateLockInfo? info);

    /// <summary>
    /// Reports an error. Receives the original <see cref="Exception"/> so the messenger can present it however suits its format.
    /// </summary>
    void ReportException(Exception exception);
}
