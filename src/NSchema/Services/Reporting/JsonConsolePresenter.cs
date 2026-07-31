using NSchema.Diff.Domain;
using NSchema.Model;
using NSchema.Plan.Domain;
using NSchema.State.Domain;

namespace NSchema.Services.Reporting;

/// <summary>
/// An <see cref="IConsolePresenter"/> that emits machine-readable output as newline-delimited JSON.
/// </summary>
internal sealed class JsonConsolePresenter : IConsolePresenter
{
    private readonly TextWriter _out;

    public JsonConsolePresenter() : this(Console.Out) { }

    internal JsonConsolePresenter(TextWriter output) => _out = output;

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
        scripts = state.Scripts
    });

    public void ReportScripts(IReadOnlyList<ScriptExecution> scripts) => JsonOutput.Write(_out, scripts.Select(s => new
    {
        name = s.Script.ToString(),
        hash = s.Hash.Value,
        executedUtc = s.ExecutedUtc
    }));
}
