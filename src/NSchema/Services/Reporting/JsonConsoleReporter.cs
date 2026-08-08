using System.Text.Json.Serialization;
using NSchema.Configuration;
using NSchema.Configuration.Plugins;
using NSchema.Diff.Domain;
using NSchema.Model;
using NSchema.Plan.Domain;
using NSchema.State.Domain;
using NSchema.State.Locks;
using Spectre.Console;

namespace NSchema.Services.Reporting;

/// <summary>
/// The <see cref="IConsoleReporter"/> for <c>--json</c>.
/// </summary>
internal sealed class JsonConsoleReporter : IConsoleReporter
{
    private readonly Verbosity _verbosity;
    private readonly TextWriter _out;
    private readonly TextWriter _error;
    private readonly Lazy<IAnsiConsole> _interaction;

    public JsonConsoleReporter(Verbosity verbosity) : this(verbosity, Console.Out, Console.Error, interaction: null) { }

    internal JsonConsoleReporter(Verbosity verbosity, TextWriter output, TextWriter error, IAnsiConsole? interaction = null)
    {
        _verbosity = verbosity;
        _out = output;
        _error = error;

        // The confirmation prompt renders on stderr — where the narration lives — so stdout stays a byte-clean
        // result stream. Built lazily: most JSON runs never prompt.
        _interaction = new Lazy<IAnsiConsole>(() => interaction ?? ConsoleFactory.Create(Console.Error, colorDisabled: false));
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

    public void Detail(ConsoleMessage message) => Report(MessageKind.Detail, message.Plain);

    // The environment banner is human-facing narration; JSON output omits it so the stream stays purely results + logs.
    public void ReportEnvironment(string? environment) { }

    public void Confirm(ConfirmationRequest request)
    {
        if (request.AutoApprove)
        {
            // Pre-approved: no interaction happens, so the confirmation reduces to narration and gates with it.
            Report(MessageKind.Announcement, request.SummaryText);
            Report(MessageKind.Announcement, "Auto-approve is enabled; skipping confirmation.");
            return;
        }

        // Interaction is never suppressed, and never touches stdout: the prompt renders on stderr.
        var console = _interaction.Value;
        console.MarkupLine(Markup.Escape(request.SummaryText));

        // Without an interactive terminal (redirected stdin / CI / a container) there is nothing to read.
        if (!console.Profile.Capabilities.Interactive)
        {
            throw new ConfirmationDeclinedException($"This operation needs confirmation, but there is no interactive terminal. Re-run with {request.SkipFlag} to proceed non-interactively.");
        }

        var response = console.Prompt(new TextPrompt<string>($"{Markup.Escape(request.Question)} Only [green]yes[/] will be accepted:").AllowEmpty());

        // Any answer other than "yes" cancels.
        if (!string.Equals(response.Trim(), "yes", StringComparison.OrdinalIgnoreCase))
        {
            throw new ConfirmationDeclinedException("Cancelled by operator. No changes were made.");
        }
    }

    public void ReportException(Exception exception) => JsonOutput.Write(_error, new ErrorEvent(
        ExceptionReport.Describe(exception),
        ExceptionReport.TypeName(exception),
        ExceptionReport.Stack(exception)));

    public void ReportDiagnostics(IReadOnlyList<Diagnostic> diagnostics)
    {
        if (diagnostics.Count == 0)
        {
            return;
        }

        // Source and Code are value objects, so they are unwrapped to their strings — serializing them whole would
        // emit `{"value":…}` wrappers. The code is what a consumer should gate on: it survives a reworded message.
        JsonOutput.Write(_out, new
        {
            type = "diagnostics",
            diagnostics = diagnostics.Select(d => new { Source = d.Source.Value, Code = d.Code.Value, d.Severity, d.Message }),
        });
    }

    public void ReportDiff(DatabaseDiff diff) => JsonOutput.Write(_out, diff);

    public void ReportDatabase(Database database) => JsonOutput.Write(_out, database);

    public void ReportPlan(MigrationPlan plan) => JsonOutput.Write(_out, new
    {
        diff = plan.Diff,
        adopted = plan.Adopted,
        sql = plan.Statements
    });

    public void ReportState(DatabaseState state) => JsonOutput.Write(_out, new
    {
        database = state.Database,
        managed = state.Managed,
        scripts = Ledger(state.Scripts)
    });

    public void ReportScripts(IReadOnlyList<ScriptExecution> scripts) => JsonOutput.Write(_out, Ledger(scripts));

    public void ReportLockInfo(StateLockInfo? info) => JsonOutput.Write(_out, info is null
        ? new LockReport(false, null, null, null, null, null)
        : new LockReport(true, info.Id.Value, info.Operation, info.Who.Value, info.CreatedUtc, info.ExpiresUtc));

    public void ReportScriptHashes(IReadOnlyList<ScriptHashEntry> scripts) => JsonOutput.Write(_out, scripts);

    // The plugin inspection commands are structured queries, so they emit a single clean object/array (not the gated
    // NDJSON log stream) — the same exception lock status makes.
    public void ReportProjectPlugins(IReadOnlyList<ProjectPlugin> plugins) => JsonOutput.Write(_out, plugins);

    public void ReportPluginDetail(ProjectPlugin plugin) => JsonOutput.Write(_out, plugin);

    public void ReportCachedPlugins(string cacheRoot, IReadOnlyList<CachedPlugin> plugins) =>
        JsonOutput.Write(_out, new { cacheRoot, plugins });

    public void ReportOutdatedPlugins(IReadOnlyList<OutdatedPlugin> plugins) => JsonOutput.Write(_out, plugins);

    // One shape for the ledger wherever it is reported, so a consumer reads `script list` and the scripts on a
    // recorded state the same way.
    private static object Ledger(IReadOnlyList<ScriptExecution> scripts) =>
        ScriptLedger.InExecutionOrder(scripts).Select(s => new
        {
            name = ScriptLedger.Name(s),
            hash = s.Hash.Value,
            executedUtc = s.ExecutedUtc
        });

    // The --json shape for a lock (lock status / lock acquire): a single object so a script can gate on `locked`
    // and read `lockId` to release it later.
    private sealed record LockReport(bool Locked, string? LockId, string? Operation, string? Who, DateTimeOffset? Since, DateTimeOffset? Expires);

    // The {"type":"error","message":…} event emitted when an operation fails.
    private sealed record ErrorEvent(string Message, string Exception, string Stack)
    {
        [JsonPropertyOrder(-1)]
        public string Type => "error";
    }
}
