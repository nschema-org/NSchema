using NSchema.Diff.Model;
using NSchema.Diff.Model.Columns;
using NSchema.Diff.Model.Schemas;
using NSchema.Diff.Model.Tables;
using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Schemas;
using NSchema.Model.Scripts;
using NSchema.Model.Tables;
using NSchema.Plan.Model;
using NSchema.Plan.PlanFile;
using NSchema.Services.Reporting;

namespace NSchema.Tests.Services;

/// <summary>
/// Snapshot coverage for <see cref="MarkdownConsolePresenter"/>.
/// </summary>
public sealed class MarkdownConsolePresenterTests
{
    private readonly StringWriter _out = new();
    private readonly MarkdownConsolePresenter _sut;

    public MarkdownConsolePresenterTests() => _sut = new MarkdownConsolePresenter(_out);

    // A diff exercising every marker: an added schema and table (+), a modified table with a type change (!) and a
    // dropped column (-), and a removed schema (-).
    private static DatabaseDiff RichDiff() => new(
    [
        new SchemaDiff("reporting", ChangeKind.Add),
        new SchemaDiff("app", Tables:
        [
            new TableDiff("app", "users", ChangeKind.Add, Columns:
            [
                new ColumnDiff("id", ChangeKind.Add, Definition: new Column { Name = "id", Type = SqlType.BigInt }),
            ]),
            new TableDiff("app", "orders", ChangeKind.Modify, Columns:
            [
                new ColumnDiff("total", ChangeKind.Modify, Type: new ValueChange<SqlType>(SqlType.Int, SqlType.BigInt)),
                new ColumnDiff("legacy", ChangeKind.Remove, Definition: new Column { Name = "legacy", Type = SqlType.Boolean }),
            ]),
        ]),
        new SchemaDiff("scratch", ChangeKind.Remove),
    ]);

    [Fact]
    public Task ReportDiff_RichDiff()
    {
        _sut.ReportDiff(RichDiff());

        return Verify(_out.ToString());
    }

    [Fact]
    public Task ReportDiff_EmptyDiff()
    {
        _sut.ReportDiff(new DatabaseDiff());

        return Verify(_out.ToString());
    }

    [Fact]
    public Task ReportDiff_WithDeploymentScripts()
    {
        // The scripts ride the diff, annotated with their deployment event; a run-once script reads the same way.
        var diff = new DatabaseDiff([new SchemaDiff("app", ChangeKind.Add)])
        {
            DeploymentScripts =
            [
                new DeploymentScript("seed-roles", "INSERT INTO app.roles VALUES ('admin');", ScopeSchema: null, DeploymentPhase.Pre) { RunCondition = RunCondition.Once },
                new DeploymentScript("refresh-views", "REFRESH MATERIALIZED VIEW app.stats;", ScopeSchema: null, DeploymentPhase.Post),
            ],
        };

        _sut.ReportDiff(diff);

        return Verify(_out.ToString());
    }

    [Fact]
    public Task ReportSqlPlan()
    {
        _sut.ReportSqlPlan(
        [
            new SqlStatement("CREATE TABLE app.users (\n    id bigint NOT NULL\n)", RunOutsideTransaction: false),
            new SqlStatement("CREATE INDEX CONCURRENTLY users_id_ix ON app.users (id)", RunOutsideTransaction: true),
        ]);

        return Verify(_out.ToString());
    }

    [Fact]
    public Task ReportSqlPlan_EmptyPlan()
    {
        _sut.ReportSqlPlan([]);

        return Verify(_out.ToString());
    }

    [Fact]
    public Task ReportSchema()
    {
        _sut.ReportSchema(new Database
        {
            Schemas =
            [
                new Schema
                {
                    Name = "app",
                    Tables = [new Table { Name = "widgets", Columns = [new Column { Name = "id", Type = SqlType.BigInt }] }],
                },
            ],
        });

        return Verify(_out.ToString());
    }

    [Fact]
    public Task ReportSavedPlan()
    {
        var diff = new DatabaseDiff([new SchemaDiff("app", ChangeKind.Add)])
        {
            DeploymentScripts = [new DeploymentScript("seed-roles", "INSERT INTO app.roles VALUES ('admin');", ScopeSchema: null, DeploymentPhase.Pre)],
        };
        var envelope = new PlanFileEnvelope(
            new MigrationPlan(diff, [new SqlStatement("CREATE TABLE app.widgets ()", RunOutsideTransaction: false)]),
            CreatedAt: default);

        _sut.ReportSavedPlan(envelope);

        return Verify(_out.ToString());
    }
}
