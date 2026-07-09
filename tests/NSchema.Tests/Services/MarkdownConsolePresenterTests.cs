using NSchema.Diff.Model;
using NSchema.Plan.Model;
using NSchema.Plan.Model.Migrations;
using NSchema.Plan.PlanFile;
using NSchema.Schema.Model;
using NSchema.Schema.Model.Columns;
using NSchema.Schema.Model.Schemas;
using NSchema.Schema.Model.Migrations;
using NSchema.Schema.Model.Scripts;
using NSchema.Schema.Model.Tables;
using NSchema.Services.Reporting;
using NSchema.Sql.Model;

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
                new ColumnDiff("id", ChangeKind.Add, Definition: new Column("id", SqlType.BigInt)),
            ]),
            new TableDiff("app", "orders", ChangeKind.Modify, Columns:
            [
                new ColumnDiff("total", ChangeKind.Modify, Type: new ValueChange<SqlType>(SqlType.Int, SqlType.BigInt)),
                new ColumnDiff("legacy", ChangeKind.Remove, Definition: new Column("legacy", SqlType.Boolean)),
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
    public Task ReportSqlPlan()
    {
        _sut.ReportSqlPlan(new SqlPlan(
        [
            new SqlStatement("CREATE TABLE app.users (\n    id bigint NOT NULL\n)"),
            new SqlStatement("CREATE INDEX CONCURRENTLY users_id_ix ON app.users (id)", RunOutsideTransaction: true),
        ]));

        return Verify(_out.ToString());
    }

    [Fact]
    public Task ReportSqlPlan_EmptyPlan()
    {
        _sut.ReportSqlPlan(new SqlPlan([]));

        return Verify(_out.ToString());
    }

    [Fact]
    public Task ReportSchema()
    {
        _sut.ReportSchema(new DatabaseSchema(
        [
            new SchemaDefinition("app", Tables: [new Table("widgets", Columns: [new Column("id", SqlType.BigInt)])]),
        ]));

        return Verify(_out.ToString());
    }

    [Fact]
    public Task ReportPlan_WithDataMigrations()
    {
        var plan = new MigrationPlan(
            [
                new ExecuteDataMigration("backfill emails", DataMigrationTrigger.AddColumn, "app", "users", "email", "UPDATE app.users SET email = '';"),
                new ExecuteDataMigration(null, DataMigrationTrigger.AddConstraint, "app", "users", "users_email_uq", "DELETE FROM app.users;"),
            ],
            [new Script("seed-roles", "INSERT INTO app.roles VALUES ('admin');", ScriptType.PreDeployment)],
            []);

        _sut.ReportPlan(plan);

        return Verify(_out.ToString());
    }

    [Fact]
    public Task ReportSavedPlan()
    {
        var envelope = new PlanFileEnvelope(
            new DatabaseDiff([new SchemaDiff("app", ChangeKind.Add)]),
            new MigrationPlan([], [new Script("seed-roles", "INSERT INTO app.roles VALUES ('admin');", ScriptType.PreDeployment)], []),
            new SqlPlan([new SqlStatement("CREATE TABLE app.widgets ()")]),
            CreatedAt: default);

        _sut.ReportSavedPlan(envelope);

        return Verify(_out.ToString());
    }
}
