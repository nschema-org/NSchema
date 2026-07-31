using NSchema.Diff.Domain;
using NSchema.Model;
using NSchema.Plan.Domain;
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

    public void ReportPlan(MigrationPlan plan)
    {
        ReportDiff(plan.Diff);
        if (!plan.Adopted.IsEmpty)
        {
            JsonOutput.Write(_out, new { type = "adoptions", adopted = plan.Adopted });
        }
        JsonOutput.Write(_out, new { type = "sqlPlan", statements = plan.Statements });
    }

    public void ReportSchema(Database database) => JsonOutput.Write(_out, database);

    public void ReportSavedPlan(PlanFileEnvelope envelope) => JsonOutput.Write(_out, new
    {
        diff = envelope.Plan.Diff,
        adopted = envelope.Plan.Adopted,
        sql = envelope.Plan.Statements,
    });
}
