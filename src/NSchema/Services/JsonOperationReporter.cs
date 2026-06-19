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

namespace NSchema.Services;

/// <summary>
/// An <see cref="IOperationReporter"/> that emits machine-readable output as newline-delimited JSON.
/// </summary>
internal sealed class JsonOperationReporter : IOperationReporter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // SQL bodies contain quotes and angle brackets; relaxed escaping keeps them readable (\" not ") — this is
        // CLI output, not HTML, so the extra-cautious default encoder isn't needed.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly RunOutcome _outcome;
    private readonly TextWriter _out;
    private readonly TextWriter _error;

    public JsonOperationReporter(RunOutcome outcome) : this(outcome, Console.Out, Console.Error) { }

    internal JsonOperationReporter(RunOutcome outcome, TextWriter output, TextWriter error)
    {
        _outcome = outcome;
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

    public void Report(MessageKind kind, string message) => Write(_error, new { type = "log", level = kind, message });

    public void ReportException(Exception exception) => Write(_error, new { type = "error", message = exception.Message });

    private static object Describe(Script script) => new { script.Name, script.Type, script.RunOutsideTransaction };

    private static void Write(TextWriter writer, object @event) => writer.WriteLine(JsonSerializer.Serialize(@event, Options));
}
