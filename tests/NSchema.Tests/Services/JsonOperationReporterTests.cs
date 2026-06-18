using System.Text.Json;
using NSchema.Diff.Model;
using NSchema.Operations;
using NSchema.Schema.Model;
using NSchema.Services;
using NSchema.Sql.Model;

namespace NSchema.Tests.Services;

public sealed class JsonOperationReporterTests
{
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();
    private readonly RunOutcome _outcome = new();
    private readonly JsonOperationReporter _sut;

    public JsonOperationReporterTests() => _sut = new JsonOperationReporter(_outcome, _out, _error);

    private List<JsonElement> StdoutEvents() => _out.ToString()
        .Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Select(line => JsonDocument.Parse(line).RootElement)
        .ToList();

    [Fact]
    public void ReportDiff_EmptyDiff_EmitsDiffEvent_AndRecordsNoChanges()
    {
        _sut.ReportDiff(new DatabaseDiff());

        var evt = StdoutEvents().ShouldHaveSingleItem();
        evt.GetProperty("type").GetString().ShouldBe("diff");
        evt.GetProperty("diff").GetProperty("isEmpty").GetBoolean().ShouldBeTrue();
        _outcome.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public void ReportDiff_NonEmptyDiff_RecordsChanges()
    {
        _sut.ReportDiff(new DatabaseDiff([new SchemaDiff("app", ChangeKind.Add)]));

        StdoutEvents().ShouldHaveSingleItem().GetProperty("diff").GetProperty("isEmpty").GetBoolean().ShouldBeFalse();
        _outcome.HasChanges.ShouldBeTrue();
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
    public void ReportSchema_EmitsSchemaEvent()
    {
        _sut.ReportSchema(new DatabaseSchema());

        StdoutEvents().ShouldHaveSingleItem().GetProperty("type").GetString().ShouldBe("schema");
    }

    [Fact]
    public void Report_Progress_GoesToStderr_LeavingStdoutClean()
    {
        _sut.Report(MessageKind.Progress, "Loading desired schema...");

        _out.ToString().ShouldBeEmpty();
        _error.ToString().ShouldContain("\"type\":\"log\"");
    }

    [Fact]
    public void Output_IsNewlineDelimited_OneObjectPerLine()
    {
        _sut.ReportDiff(new DatabaseDiff());
        _sut.ReportSchema(new DatabaseSchema());

        StdoutEvents().Select(e => e.GetProperty("type").GetString()).ShouldBe(["diff", "schema"]);
    }
}
