using NSchema.Diff.Model;
using NSchema.Plan.Model;
using NSchema.Plan.Model.Migrations;
using NSchema.Plan.PlanFile;
using NSchema.Schema.Model;
using NSchema.Schema.Model.Scripts;
using NSchema.Sql.Model;

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

    public void ReportSchema(DatabaseSchema schema) => JsonOutput.Write(_out, schema);

    public void ReportSqlPlan(SqlPlan plan) => JsonOutput.Write(_out, new { type = "sqlPlan", statements = plan.Statements });

    public void ReportSavedPlan(PlanFileEnvelope envelope) => JsonOutput.Write(_out, new
    {
        diff = envelope.Diff,
        scripts = new
        {
            preDeployment = envelope.Plan.PreDeploymentScripts.Select(Describe),
            postDeployment = envelope.Plan.PostDeploymentScripts.Select(Describe),
        },
        sql = envelope.Sql.Statements,
    });

    public void ReportPlan(MigrationPlan plan)
    {
        var migrations = plan.Actions.OfType<ExecuteDataMigration>().ToList();
        if (plan.PreDeploymentScripts.Count == 0 && plan.PostDeploymentScripts.Count == 0 && migrations.Count == 0)
        {
            return;
        }

        JsonOutput.Write(_out, new
        {
            type = "scripts",
            preDeployment = plan.PreDeploymentScripts.Select(Describe),
            postDeployment = plan.PostDeploymentScripts.Select(Describe),
            dataMigrations = migrations.Select(Describe),
        });
    }

    private static object Describe(Script script) => new { script.Name, script.Type, script.RunOutsideTransaction, script.RunCondition };

    private static object Describe(ExecuteDataMigration migration) => new
    {
        migration.Name,
        migration.Trigger,
        Path = $"{migration.SchemaName}.{migration.TableName}.{migration.MemberName}",
        migration.RunOutsideTransaction,
    };
}
