using NSchema.Diff.Model;
using NSchema.Operations;
using NSchema.Plan.Model;
using NSchema.Policies;
using NSchema.Schema.Model;
using NSchema.Schema.Model.Scripts;
using NSchema.Sql.Model;

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

    // A detail line is secondary narration, so it joins the gated log stream (on stderr) like other messages.
    public void Detail(string message) => Report(MessageKind.Announcement, message);

    public void ReportException(Exception exception) => Write(_error, new ErrorEvent(exception.Message));

    private static object Describe(Script script) => new { script.Name, script.Type, script.RunOutsideTransaction };

    private static void Write(TextWriter writer, object @event) => JsonOutput.Write(writer, @event);
}
