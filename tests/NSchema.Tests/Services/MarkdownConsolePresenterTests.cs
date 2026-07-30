using NSchema.Diff.Domain;
using NSchema.Diff.Domain.Columns;
using NSchema.Diff.Domain.Schemas;
using NSchema.Diff.Domain.Tables;
using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Schemas;
using NSchema.Model.Scripts;
using NSchema.Model.Tables;
using NSchema.Plan.Domain;
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
    // dropped column (-), and a removed schema (-). 'app' is Touched — in the diff only because its tables changed —
    // so it contributes its contents and no header line of its own.
    private static DatabaseDiff RichDiff() => new(
    [
        SchemaDiff.Added("reporting"),
        SchemaDiff.Containing("app") with
        {
            Tables =
            [
                TableDiff.Added("app", new Table { Name = "users" }) with
                {
                    Columns = [ColumnDiff.Added(new Column { Name = "id", Type = SqlType.BigInt })],
                },
                TableDiff.Modified("app", "orders") with
                {
                    Columns =
                    [
                        ColumnDiff.Modified(new Column { Name = "total", Type = SqlType.BigInt })
                            with { Type = new ValueChange<SqlType>(SqlType.Int, SqlType.BigInt) },
                        ColumnDiff.Removed(new Column { Name = "legacy", Type = SqlType.Boolean }),
                    ],
                },
            ],
        },
        SchemaDiff.Removed("scratch"),
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
    public Task ReportDiff_TouchedSchema()
    {
        // A schema carried by its contents alone renders no header — only what actually changed inside it. Pinned on
        // its own (RichDiff covers it among four other markers) so a regression names the case it broke.
        var diff = new DatabaseDiff(
        [
            SchemaDiff.Containing("app") with
            {
                Tables =
                [
                    TableDiff.Modified("app", "orders") with
                    {
                        Columns = [ColumnDiff.Added(new Column { Name = "placed_at", Type = SqlType.BigInt })],
                    },
                ],
            },
        ]);

        _sut.ReportDiff(diff);

        return Verify(_out.ToString());
    }

    [Fact]
    public Task ReportDiff_WithDeploymentScripts()
    {
        // The scripts ride the diff, annotated with their deployment event; a run-once script reads the same way.
        var diff = new DatabaseDiff([SchemaDiff.Added("app")])
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
        var diff = new DatabaseDiff([SchemaDiff.Added("app")])
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
