using NSchema.Operations;
using NSchema.State.Model;

namespace NSchema.Services;

/// <summary>
/// The CLI's single presentation surface: a superset of the core <see cref="IOperationReporter"/>.
/// </summary>
internal interface IConsolePresenter : IOperationReporter
{
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
    /// Prints which environment a run is targeting.
    /// </summary>
    void ReportEnvironment(string? environment);

    /// <summary>
    /// Writes an indented secondary line beneath a headline (e.g. the lock id and expiry under a <c>lock status</c> line).
    /// </summary>
    void Detail(string message);

    /// <summary>
    /// An indented secondary line.
    /// </summary>
    void Detail(ConsoleMessage message);

    /// <summary>
    /// Reports the state-lock status.
    /// </summary>
    void ReportLockStatus(StateLockInfo? info);
}
