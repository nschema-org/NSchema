using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using NSchema.Diff.Model;
using NSchema.Operations;
using NSchema.Plan.Model;
using NSchema.Policies;
using NSchema.Schema.Model;
using NSchema.Schema.Model.Scripts;
using NSchema.Sql.Model;
using NSchema.State.Model;

namespace NSchema.Services;

/// <summary>
/// An <see cref="IConsolePresenter"/> that emits machine-readable output as newline-delimited JSON.
/// </summary>
internal sealed class JsonConsolePresenter : IConsolePresenter
{
    private readonly RunOutcome _outcome;
    private readonly OutputVerbosity _verbosity;
    private readonly TextWriter _out;
    private readonly TextWriter _error;

    public JsonConsolePresenter(RunOutcome outcome, OutputVerbosity verbosity) : this(outcome, verbosity, Console.Out, Console.Error) { }

    internal JsonConsolePresenter(RunOutcome outcome, OutputVerbosity verbosity, TextWriter output, TextWriter error)
    {
        _outcome = outcome;
        _verbosity = verbosity;
        _out = output;
        _error = error;
    }

    public void ReportDiff(DatabaseDiff diff)
    {
        _outcome.HasChanges = !diff.IsEmpty;
        Write(_out, new { type = "diff", diff });
    }

    public void ReportSchema(DatabaseSchema schema) => Write(_out, new { type = "schema", schema });

    public void ReportSqlPlan(SqlPlan plan) => Write(_out, new { type = "sqlPlan", statements = plan.Statements });

    public void ReportPlan(MigrationPlan plan)
    {
        if (plan.PreDeploymentScripts.Count == 0 && plan.PostDeploymentScripts.Count == 0)
        {
            return;
        }

        Write(_out, new
        {
            type = "scripts",
            preDeployment = plan.PreDeploymentScripts.Select(Describe),
            postDeployment = plan.PostDeploymentScripts.Select(Describe),
        });
    }

    public void ReportDiagnostics(PolicyDiagnostics diagnostics)
    {
        if (diagnostics.Count == 0)
        {
            return;
        }

        Write(_out, new { type = "diagnostics", diagnostics = diagnostics.ToList() });
    }

    public void Report(MessageKind kind, string message)
    {
        // Gate the log stream by verbosity too, so --quiet / --verbose mean the same thing in NDJSON mode.
        // The structured events (diff, sqlPlan, schema, diagnostics) are the results and are never gated.
        if (!_verbosity.ShouldShow(kind))
        {
            return;
        }

        Write(_error, new { type = "log", level = kind, message });
    }

    public void Announce(ConsoleMessage message) => Report(MessageKind.Announcement, message.Plain);

    public void Progress(ConsoleMessage message) => Report(MessageKind.Progress, message.Plain);

    public void Success(ConsoleMessage message) => Report(MessageKind.Success, message.Plain);

    public void Warn(ConsoleMessage message) => Report(MessageKind.Warning, message.Plain);

    // A detail line is secondary narration, so it joins the gated log stream (on stderr) like other messages.
    public void Detail(string message) => Report(MessageKind.Announcement, message);

    public void Detail(ConsoleMessage message) => Detail(message.Plain);

    public void ReportException(Exception exception) => Write(_error, new ErrorEvent(exception.Message));

    // The environment banner is human-facing narration; JSON output omits it so the stream stays purely results + logs.
    public void ReportEnvironment(string? environment) { }

    public void ReportLockStatus(StateLockInfo? info) => Write(_out, info is null
        ? new LockStatusReport(false, null, null, null, null, null)
        : new LockStatusReport(true, info.Id, info.Operation, info.Who, info.CreatedUtc, info.ExpiresUtc));

    private static object Describe(Script script) => new { script.Name, script.Type, script.RunOutsideTransaction };

    // The --json shape for lock status: a single object so a script can gate on `locked` and read `lockId`.
    private sealed record LockStatusReport(bool Locked, string? LockId, string? Operation, string? Who, DateTimeOffset? Since, DateTimeOffset? Expires);

    // The {"type":"error","message":…} event emitted when an operation fails.
    private sealed record ErrorEvent(string Message)
    {
        [JsonPropertyOrder(-1)]
        public string Type => "error";
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // SQL bodies contain quotes and angle brackets; relaxed escaping keeps them readable (\" not ") — this is
        // CLI output, not HTML, so the extra-cautious default encoder isn't needed.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private static void Write(TextWriter writer, object @event) => writer.WriteLine(JsonSerializer.Serialize(@event, SerializerOptions));
}
