using NSchema.Configuration.Plugins;
using NSchema.Diff.Domain;
using NSchema.Model;
using NSchema.Plan.Domain;
using NSchema.State.Domain;
using NSchema.State.Locks;

namespace NSchema.Services.Reporting;

/// <summary>
/// Provides an abstraction over the CLI's console output.
/// </summary>
internal interface IConsoleReporter
{
    // ── Narration ─────────────────────────────────────────────────────────────────────────────────────────────────

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
    /// An indented secondary line elaborating on the message before it.
    /// </summary>
    void Detail(ConsoleMessage message);

    /// <summary>
    /// Prints which environment a run is targeting.
    /// </summary>
    void ReportEnvironment(string? environment);

    // ── Interaction ───────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Presents what is about to happen and requires the operator to type "yes" before continuing, unless the request is pre-approved.
    /// </summary>
    /// <exception cref="ConfirmationDeclinedException">Thrown when the operator declines, or the terminal is not interactive.</exception>
    void Confirm(ConfirmationRequest request);

    // ── Errors — never gated ──────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Reports an error. Receives the original <see cref="Exception"/> so the reporter can present it however suits its format.
    /// </summary>
    void ReportException(Exception exception);

    // ── Artifacts ─────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Reports the diagnostics (warnings, info, and errors) produced during an operation.
    /// </summary>
    void ReportDiagnostics(IReadOnlyList<Diagnostic> diagnostics);

    /// <summary>
    /// Presents a database schema.
    /// </summary>
    void ReportDatabase(Database database);

    /// <summary>
    /// Presents the recorded state.
    /// </summary>
    void ReportState(DatabaseState state);

    /// <summary>
    /// Presents the computed migration diff.
    /// </summary>
    void ReportDiff(DatabaseDiff diff);

    /// <summary>
    /// Presents the computed plan.
    /// </summary>
    void ReportPlan(MigrationPlan plan);

    /// <summary>
    /// Presents the script executions recorded in the state ledger.
    /// </summary>
    void ReportScripts(IReadOnlyList<ScriptExecution> scripts);

    /// <summary>
    /// Reports the information about a lock.
    /// </summary>
    void ReportLockInfo(StateLockInfo? info);

    /// <summary>
    /// Reports the scripts a project declares, with their body hashes.
    /// </summary>
    void ReportScriptHashes(IReadOnlyList<ScriptHashEntry> scripts);

    /// <summary>
    /// Reports the plugins a project pins (database and state), annotated with their cache status.
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
    /// Reports each project plugin's pinned, range-wanted, and latest available versions.
    /// </summary>
    void ReportOutdatedPlugins(IReadOnlyList<OutdatedPlugin> plugins);
}
