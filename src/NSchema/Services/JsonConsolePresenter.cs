using NSchema.Diff.Model;
using NSchema.Plan.Model;
using NSchema.Policies;
using NSchema.Schema.Model;
using NSchema.Schema.Model.Scripts;
using NSchema.Sql.Model;

namespace NSchema.Services;

/// <summary>
/// An <see cref="IConsolePresenter"/> that emits machine-readable output as newline-delimited JSON.
/// </summary>
internal sealed class JsonConsolePresenter : JsonConsoleMessenger, IConsolePresenter
{
    public JsonConsolePresenter(Verbosity verbosity) : base(verbosity) { }

    internal JsonConsolePresenter(Verbosity verbosity, TextWriter output, TextWriter error) : base(verbosity, output, error) { }

    public void ReportDiff(DatabaseDiff diff) => Write(Out, new { type = "diff", diff });

    public void ReportSchema(DatabaseSchema schema) => Write(Out, new { type = "schema", schema });

    public void ReportSqlPlan(SqlPlan plan) => Write(Out, new { type = "sqlPlan", statements = plan.Statements });

    public void ReportPlan(MigrationPlan plan)
    {
        if (plan.PreDeploymentScripts.Count == 0 && plan.PostDeploymentScripts.Count == 0)
        {
            return;
        }

        Write(Out, new
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

        Write(Out, new { type = "diagnostics", diagnostics = diagnostics.ToList() });
    }

    private static object Describe(Script script) => new { script.Name, script.Type, script.RunOutsideTransaction };
}
