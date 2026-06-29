using System.Text.Json;
using NSchema.Diff.Model;
using NSchema.Plan.Model;
using NSchema.Plan.PlanFile;
using NSchema.Schema.Model;
using NSchema.Schema.Model.Scripts;
using NSchema.Services.Reporting;
using NSchema.Sql.Model;

namespace NSchema.Tests.Services;

public sealed class JsonConsolePresenterTests
{
    private readonly StringWriter _out = new();
    private readonly JsonConsolePresenter _sut;

    public JsonConsolePresenterTests() => _sut = new JsonConsolePresenter(_out);

    private List<JsonElement> StdoutEvents() => _out.ToString()
        .Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Select(line => JsonDocument.Parse(line).RootElement)
        .ToList();

    [Fact]
    public void ReportDiff_EmptyDiff_EmitsDiffEvent()
    {
        _sut.ReportDiff(new DatabaseDiff());

        var evt = StdoutEvents().ShouldHaveSingleItem();
        evt.GetProperty("type").GetString().ShouldBe("diff");
        evt.GetProperty("diff").GetProperty("isEmpty").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public void ReportDiff_NonEmptyDiff_EmitsDiffEvent()
    {
        _sut.ReportDiff(new DatabaseDiff([new SchemaDiff("app", ChangeKind.Add)]));

        StdoutEvents().ShouldHaveSingleItem().GetProperty("diff").GetProperty("isEmpty").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public void ReportSqlPlan_EmitsStatementsWithTransactionFlag()
    {
        _sut.ReportSqlPlan(new SqlPlan([new SqlStatement("CREATE INDEX CONCURRENTLY i ON t (c)", RunOutsideTransaction: true)]));

        var evt = StdoutEvents().ShouldHaveSingleItem();
        evt.GetProperty("type").GetString().ShouldBe("sqlPlan");
        var statement = evt.GetProperty("statements")[0];
        statement.GetProperty("sql").GetString()!.ShouldContain("CONCURRENTLY");
        statement.GetProperty("runOutsideTransaction").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public void ReportSchema_EmitsBareSchemaObject_WithNoTypeEnvelope()
    {
        // A `show` is a single query, so the schema is the whole object — no NDJSON "type" discriminator to filter past.
        _sut.ReportSchema(new DatabaseSchema());

        var evt = StdoutEvents().ShouldHaveSingleItem();
        evt.ValueKind.ShouldBe(JsonValueKind.Object);
        evt.TryGetProperty("type", out _).ShouldBeFalse();
    }

    [Fact]
    public void ReportSavedPlan_EmitsBareCompositeObject_WithDiffScriptsAndSql()
    {
        var envelope = new PlanFileEnvelope(
            new DatabaseDiff([new SchemaDiff("app", ChangeKind.Add)]),
            new MigrationPlan([], [new Script("seed-roles", "INSERT INTO app.roles VALUES ('admin');", ScriptType.PreDeployment)], []),
            new SqlPlan([new SqlStatement("CREATE TABLE app.widgets ()", RunOutsideTransaction: false)]),
            CreatedAt: default);

        _sut.ReportSavedPlan(envelope);

        // One bare object the whole `plan show` answer lives in — no "type" envelope, no multi-line stream to slurp.
        var evt = StdoutEvents().ShouldHaveSingleItem();
        evt.TryGetProperty("type", out _).ShouldBeFalse();
        evt.GetProperty("diff").GetProperty("isEmpty").GetBoolean().ShouldBeFalse();
        evt.GetProperty("scripts").GetProperty("preDeployment")[0].GetProperty("name").GetString().ShouldBe("seed-roles");
        evt.GetProperty("scripts").GetProperty("postDeployment").GetArrayLength().ShouldBe(0);
        evt.GetProperty("sql")[0].GetProperty("sql").GetString()!.ShouldContain("CREATE TABLE app.widgets");
    }

    [Fact]
    public void Output_IsNewlineDelimited_OneObjectPerLine()
    {
        // The streaming methods (used by apply/plan/destroy/drift) frame each event as its own NDJSON line.
        _sut.ReportDiff(new DatabaseDiff());
        _sut.ReportSqlPlan(new SqlPlan([]));

        StdoutEvents().Select(e => e.GetProperty("type").GetString()).ShouldBe(["diff", "sqlPlan"]);
    }
}
