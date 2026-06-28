using System.Text.Json;
using NSchema.Diff.Model;
using NSchema.Schema.Model;
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
    public void ReportSchema_EmitsSchemaEvent()
    {
        _sut.ReportSchema(new DatabaseSchema());

        StdoutEvents().ShouldHaveSingleItem().GetProperty("type").GetString().ShouldBe("schema");
    }

    [Fact]
    public void Output_IsNewlineDelimited_OneObjectPerLine()
    {
        _sut.ReportDiff(new DatabaseDiff());
        _sut.ReportSchema(new DatabaseSchema());

        StdoutEvents().Select(e => e.GetProperty("type").GetString()).ShouldBe(["diff", "schema"]);
    }
}
