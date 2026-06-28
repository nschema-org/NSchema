using System.Text.Json.Serialization;
using NSchema.Configuration.Plugins;
using NSchema.Policies;
using NSchema.State.Model;

namespace NSchema.Services.Reporting;

/// <summary>
/// Emits line-level narration and outcomes as newline-delimited JSON.
/// </summary>
internal sealed class JsonConsoleMessenger : IConsoleMessenger
{
    private readonly Verbosity _verbosity;
    private readonly TextWriter _out;
    private readonly TextWriter _error;

    public JsonConsoleMessenger(Verbosity verbosity) : this(verbosity, Console.Out, Console.Error) { }

    internal JsonConsoleMessenger(Verbosity verbosity, TextWriter output, TextWriter error)
    {
        _verbosity = verbosity;
        _out = output;
        _error = error;
    }

    public void Report(MessageKind kind, string message)
    {
        // Gate the log stream by verbosity too, so --quiet / --verbose mean the same thing in NDJSON mode.
        // The structured events (diff, sqlPlan, schema, diagnostics) are the results and are never gated.
        if (!_verbosity.ShouldShow(kind))
        {
            return;
        }

        JsonOutput.Write(_error, new { type = "log", level = kind, message });
    }

    public void Announce(ConsoleMessage message) => Report(MessageKind.Announcement, message.Plain);

    public void Success(ConsoleMessage message) => Report(MessageKind.Success, message.Plain);

    public void Warn(ConsoleMessage message) => Report(MessageKind.Warning, message.Plain);

    public void Detail(ConsoleMessage message) => Report(MessageKind.Announcement, message.Plain);

    public void ReportException(Exception exception) => JsonOutput.Write(_error, new ErrorEvent(exception.Message));

    // The environment banner is human-facing narration; JSON output omits it so the stream stays purely results + logs.
    public void ReportEnvironment(string? environment) { }

    public void ReportLockInfo(StateLockInfo? info) => JsonOutput.Write(_out, info is null
        ? new LockReport(false, null, null, null, null, null)
        : new LockReport(true, info.Id, info.Operation, info.Who, info.CreatedUtc, info.ExpiresUtc));

    // The plugin inspection commands are structured queries, so they emit a single clean object/array (not the gated
    // NDJSON log stream) — the same exception lock status makes.
    public void ReportProjectPlugins(IReadOnlyList<ProjectPlugin> plugins) => JsonOutput.Write(_out, plugins);

    public void ReportPluginDetail(ProjectPlugin plugin) => JsonOutput.Write(_out, plugin);

    public void ReportCachedPlugins(string cacheRoot, IReadOnlyList<CachedPlugin> plugins) =>
        JsonOutput.Write(_out, new { cacheRoot, plugins });

    public void ReportDiagnostics(PolicyDiagnostics diagnostics)
    {
        if (diagnostics.Count == 0)
        {
            return;
        }

        JsonOutput.Write(_out, new { type = "diagnostics", diagnostics = diagnostics.ToList() });
    }

    // The --json shape for a lock (lock status / lock acquire): a single object so a script can gate on `locked`
    // and read `lockId` to release it later.
    private sealed record LockReport(bool Locked, string? LockId, string? Operation, string? Who, DateTimeOffset? Since, DateTimeOffset? Expires);

    // The {"type":"error","message":…} event emitted when an operation fails.
    private sealed record ErrorEvent(string Message)
    {
        [JsonPropertyOrder(-1)]
        public string Type => "error";
    }
}
