using System.Text.Json;
using NSchema.Diff.Model;
using NSchema.Operations;
using NSchema.Schema.Model;
using NSchema.Services;
using NSchema.Sql.Model;

namespace NSchema.Tests.Services;

public sealed class JsonConsolePresenterTests
{
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();
    private readonly RunOutcome _outcome = new();
    private readonly JsonConsolePresenter _sut;

    public JsonConsolePresenterTests() => _sut = Build(Verbosity.Normal);

    private JsonConsolePresenter Build(Verbosity verbosity) =>
        new(_outcome, new OutputVerbosity(verbosity), _out, _error);

    private List<JsonElement> StderrEvents() => _error.ToString()
        .Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Select(line => JsonDocument.Parse(line).RootElement)
        .ToList();

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
    public void Report_NormalVerbosity_SuppressesVerboseLogEvents()
    {
        _sut.Report(MessageKind.Verbose, "Read 2 DDL files.");

        _error.ToString().ShouldBeEmpty();
    }

    [Fact]
    public void Report_VerboseVerbosity_EmitsVerboseLogEventWithLevel()
    {
        Build(Verbosity.Verbose).Report(MessageKind.Verbose, "Read 2 DDL files.");

        var evt = StderrEvents().ShouldHaveSingleItem();
        evt.GetProperty("type").GetString().ShouldBe("log");
        evt.GetProperty("level").GetString().ShouldBe("verbose");
    }

    [Fact]
    public void Report_QuietVerbosity_SuppressesProgressButKeepsStructuredResults()
    {
        var quiet = Build(Verbosity.Quiet);

        quiet.Report(MessageKind.Progress, "Loading desired schema...");
        quiet.ReportDiff(new DatabaseDiff([new SchemaDiff("app", ChangeKind.Add)]));

        _error.ToString().ShouldBeEmpty();
        StdoutEvents().ShouldHaveSingleItem().GetProperty("type").GetString().ShouldBe("diff");
    }

    [Fact]
    public void Success_Interpolated_EmitsUnstyledPlainText()
    {
        // Act — the highlighting overload still emits plain text for JSON (no markup in the message).
        var package = "postgres";
        _sut.Success($"Restored {package} now");

        // Assert
        var evt = StderrEvents().ShouldHaveSingleItem();
        evt.GetProperty("type").GetString().ShouldBe("log");
        evt.GetProperty("level").GetString().ShouldBe("success");
        evt.GetProperty("message").GetString().ShouldBe("Restored postgres now");
    }

    [Fact]
    public void Output_IsNewlineDelimited_OneObjectPerLine()
    {
        _sut.ReportDiff(new DatabaseDiff());
        _sut.ReportSchema(new DatabaseSchema());

        StdoutEvents().Select(e => e.GetProperty("type").GetString()).ShouldBe(["diff", "schema"]);
    }
}
