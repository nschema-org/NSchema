using System.Text.Json;
using NSchema.Diff.Model;
using NSchema.Diff.Model.Schemas;
using NSchema.Model;
using NSchema.Model.Scripts;
using NSchema.Plan.Model;
using NSchema.Plan.PlanFile;
using NSchema.Services.Reporting;

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
    public void ReportDiff_CarriesTheDeploymentScriptsOnTheDiff()
    {
        // The scripts are first-class on the diff now, so the diff event carries them — no separate scripts event.
        var diff = new DatabaseDiff([new SchemaDiff("app", ChangeKind.Add)])
        {
            DeploymentScripts =
            [
                new DeploymentScript("seed-roles", "INSERT INTO app.roles VALUES ('admin');", ScopeSchema: null, DeploymentPhase.Pre)
                {
                    RunCondition = RunCondition.Once,
                },
            ],
        };

        _sut.ReportDiff(diff);

        var script = StdoutEvents().ShouldHaveSingleItem().GetProperty("diff").GetProperty("deploymentScripts")[0];
        script.GetProperty("name").GetString().ShouldBe("seed-roles");
        script.GetProperty("phase").GetString().ShouldBe("pre");
        script.GetProperty("runCondition").GetString().ShouldBe("once");
    }

    [Fact]
    public void ReportSqlPlan_EmitsStatementsWithTransactionFlag()
    {
        _sut.ReportSqlPlan([new SqlStatement("CREATE INDEX CONCURRENTLY i ON t (c)", RunOutsideTransaction: true)]);

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
        _sut.ReportSchema(new Database());

        var evt = StdoutEvents().ShouldHaveSingleItem();
        evt.ValueKind.ShouldBe(JsonValueKind.Object);
        evt.TryGetProperty("type", out _).ShouldBeFalse();
    }

    [Fact]
    public void ReportSavedPlan_EmitsBareCompositeObject_WithDiffAndSql()
    {
        var diff = new DatabaseDiff([new SchemaDiff("app", ChangeKind.Add)])
        {
            DeploymentScripts = [new DeploymentScript("seed-roles", "INSERT INTO app.roles VALUES ('admin');", ScopeSchema: null, DeploymentPhase.Pre)],
        };
        var envelope = new PlanFileEnvelope(
            new MigrationPlan(diff, [new SqlStatement("CREATE TABLE app.widgets ()", RunOutsideTransaction: false)]),
            CreatedAt: default);

        _sut.ReportSavedPlan(envelope);

        // One bare object the whole `plan show` answer lives in — no "type" envelope, no multi-line stream to slurp.
        var evt = StdoutEvents().ShouldHaveSingleItem();
        evt.TryGetProperty("type", out _).ShouldBeFalse();
        evt.GetProperty("diff").GetProperty("isEmpty").GetBoolean().ShouldBeFalse();
        evt.GetProperty("diff").GetProperty("deploymentScripts")[0].GetProperty("name").GetString().ShouldBe("seed-roles");
        evt.GetProperty("sql")[0].GetProperty("sql").GetString()!.ShouldContain("CREATE TABLE app.widgets");
    }

    [Fact]
    public void Output_IsNewlineDelimited_OneObjectPerLine()
    {
        // The streaming methods (used by apply/plan/destroy/drift) frame each event as its own NDJSON line.
        _sut.ReportDiff(new DatabaseDiff());
        _sut.ReportSqlPlan([]);

        StdoutEvents().Select(e => e.GetProperty("type").GetString()).ShouldBe(["diff", "sqlPlan"]);
    }
}
