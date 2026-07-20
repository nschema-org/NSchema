using NSchema.Diff.Model;
using NSchema.Model;
using NSchema.Plan.Model;
using NSchema.Plan.PlanFile;

namespace NSchema.Services.Reporting;

/// <summary>
/// An <see cref="IConsolePresenter"/> that emits machine-readable output as newline-delimited JSON.
/// </summary>
internal sealed class JsonConsolePresenter : IConsolePresenter
{
    private readonly TextWriter _out;

    public JsonConsolePresenter() : this(Console.Out) { }

    internal JsonConsolePresenter(TextWriter output) => _out = output;

    public void ReportDiff(DatabaseDiff diff) => JsonOutput.Write(_out, new { type = "diff", diff });

    public void ReportSchema(Database database) => JsonOutput.Write(_out, database);

    public void ReportSqlPlan(IReadOnlyList<SqlStatement> statements) => JsonOutput.Write(_out, new { type = "sqlPlan", statements });

    public void ReportSavedPlan(PlanFileEnvelope envelope) => JsonOutput.Write(_out, new
    {
        diff = envelope.Plan.Diff,
        sql = envelope.Plan.Statements,
    });
}
