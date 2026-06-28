using NSchema.Configuration.Plugins;
using NSchema.Policies;
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
    /// Reports a success outcome.
    /// </summary>
    void Success(ConsoleMessage message);

    /// <summary>
    /// Reports a warning.
    /// </summary>
    void Warn(ConsoleMessage message);

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
    /// Reports the plugins a project pins (provider and backend), annotated with their cache status.
    /// </summary>
    void ReportProjectPlugins(IReadOnlyList<ProjectPlugin> plugins);

    /// <summary>
    /// Reports the detail of a single project plugin.
    /// </summary>
    void ReportPluginDetail(ProjectPlugin plugin);

    /// <summary>
    /// Reports the restored plugins currently held in the global plugin cache.
    /// </summary>
    void ReportCachedPlugins(string cacheRoot, IReadOnlyList<CachedPlugin> plugins);

    /// <summary>
    /// Reports an error. Receives the original <see cref="Exception"/> so the messenger can present it however suits its format.
    /// </summary>
    void ReportException(Exception exception);

    /// <summary>
    /// Reports policy diagnostics (warnings, info, and errors) produced during an operation.
    /// </summary>
    void ReportDiagnostics(PolicyDiagnostics diagnostics);
}
