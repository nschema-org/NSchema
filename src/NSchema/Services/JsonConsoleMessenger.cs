using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using NSchema.Configuration.Plugins;
using NSchema.Operations;
using NSchema.State.Model;

namespace NSchema.Services;

/// <summary>
/// Emits line-level narration and outcomes as newline-delimited JSON.
/// </summary>
internal class JsonConsoleMessenger : IConsoleMessenger
{
    private readonly Verbosity _verbosity;
    protected readonly TextWriter Out;
    protected readonly TextWriter Error;

    public JsonConsoleMessenger(Verbosity verbosity) : this(verbosity, Console.Out, Console.Error) { }

    internal JsonConsoleMessenger(Verbosity verbosity, TextWriter output, TextWriter error)
    {
        _verbosity = verbosity;
        Out = output;
        Error = error;
    }

    public void Report(MessageKind kind, string message)
    {
        // Gate the log stream by verbosity too, so --quiet / --verbose mean the same thing in NDJSON mode.
        // The structured events (diff, sqlPlan, schema, diagnostics) are the results and are never gated.
        if (!_verbosity.ShouldShow(kind))
        {
            return;
        }

        Write(Error, new { type = "log", level = kind, message });
    }

    public void Announce(ConsoleMessage message) => Report(MessageKind.Announcement, message.Plain);

    public void Progress(ConsoleMessage message) => Report(MessageKind.Progress, message.Plain);

    public void Success(ConsoleMessage message) => Report(MessageKind.Success, message.Plain);

    public void Warn(ConsoleMessage message) => Report(MessageKind.Warning, message.Plain);

    // A detail line is secondary narration, so it joins the gated log stream (on stderr) like other messages.
    public void Detail(string message) => Report(MessageKind.Announcement, message);

    public void Detail(ConsoleMessage message) => Detail(message.Plain);

    public void ReportException(Exception exception) => Write(Error, new ErrorEvent(exception.Message));

    // The environment banner is human-facing narration; JSON output omits it so the stream stays purely results + logs.
    public void ReportEnvironment(string? environment) { }

    public void ReportLockInfo(StateLockInfo? info) => Write(Out, info is null
        ? new LockReport(false, null, null, null, null, null)
        : new LockReport(true, info.Id, info.Operation, info.Who, info.CreatedUtc, info.ExpiresUtc));

    // The plugin inspection commands are structured queries, so they emit a single clean object/array (not the gated
    // NDJSON log stream) — the same exception lock status makes.
    public void ReportProjectPlugins(IReadOnlyList<ProjectPlugin> plugins) => Write(Out, plugins);

    public void ReportPluginDetail(ProjectPlugin plugin) => Write(Out, plugin);

    public void ReportCachedPlugins(string cacheRoot, IReadOnlyList<CachedPlugin> plugins) =>
        Write(Out, new { cacheRoot, plugins });

    // The --json shape for a lock (lock status / lock acquire): a single object so a script can gate on `locked`
    // and read `lockId` to release it later.
    private sealed record LockReport(bool Locked, string? LockId, string? Operation, string? Who, DateTimeOffset? Since, DateTimeOffset? Expires);

    // The {"type":"error","message":…} event emitted when an operation fails.
    private sealed record ErrorEvent(string Message)
    {
        [JsonPropertyOrder(-1)]
        public string Type => "error";
    }

    protected static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // SQL bodies contain quotes and angle brackets; relaxed escaping keeps them readable (\" not ") — this is
        // CLI output, not HTML, so the extra-cautious default encoder isn't needed.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    protected static void Write(TextWriter writer, object @event) => writer.WriteLine(JsonSerializer.Serialize(@event, SerializerOptions));
}
