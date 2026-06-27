using System.Text.Json;
using NSchema.Configuration.Plugins;
using NSchema.Diff.Model;
using NSchema.Operations;
using NSchema.Schema.Model;
using NSchema.Services;
using NSchema.Sql.Model;
using NSchema.State.Model;

namespace NSchema.Tests.Services;

public sealed class JsonConsolePresenterTests
{
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();
    private readonly JsonConsolePresenter _sut;

    public JsonConsolePresenterTests() => _sut = Build(Verbosity.Normal);

    private JsonConsolePresenter Build(Verbosity verbosity) =>
        new(verbosity, _out, _error);

    private List<JsonElement> StderrEvents() => _error.ToString()
        .Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Select(line => JsonDocument.Parse(line).RootElement)
        .ToList();

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
    public void ReportLockInfo_Null_EmitsLockedFalseObject()
    {
        _sut.ReportLockInfo(null);

        // Null members are omitted, so the absence of a lock is simply {"locked":false}.
        var evt = StdoutEvents().ShouldHaveSingleItem();
        evt.GetProperty("locked").GetBoolean().ShouldBeFalse();
        evt.TryGetProperty("lockId", out _).ShouldBeFalse();
    }

    [Fact]
    public void ReportLockInfo_Held_EmitsLockObject()
    {
        // The same machine-readable object backs lock status and lock acquire, so a script can read the id.
        var info = new StateLockInfo("abc", "apply", "tom@dev", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(30));

        _sut.ReportLockInfo(info);

        var evt = StdoutEvents().ShouldHaveSingleItem();
        evt.GetProperty("locked").GetBoolean().ShouldBeTrue();
        evt.GetProperty("lockId").GetString().ShouldBe("abc");
        evt.GetProperty("operation").GetString().ShouldBe("apply");
        evt.GetProperty("who").GetString().ShouldBe("tom@dev");
        evt.GetProperty("expires").ValueKind.ShouldNotBe(JsonValueKind.Null);
    }

    [Fact]
    public void ReportProjectPlugins_EmitsArrayWithRoleAndCacheStatus()
    {
        // Arrange
        var plugins = new[]
        {
            new ProjectPlugin("provider", "postgres", "NSchema.Postgres", "4.0.0", Restored: true, CachePath: "/c"),
        };

        // Act
        _sut.ReportProjectPlugins(plugins);

        // Assert — a single clean array (a structured query result, not the NDJSON log stream).
        var array = StdoutEvents().ShouldHaveSingleItem();
        array.ValueKind.ShouldBe(JsonValueKind.Array);
        array[0].GetProperty("role").GetString().ShouldBe("provider");
        array[0].GetProperty("label").GetString().ShouldBe("postgres");
        array[0].GetProperty("restored").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public void ReportCachedPlugins_EmitsRootAndPluginsArray()
    {
        // Arrange
        var cached = new[] { new CachedPlugin("NSchema.Postgres", "4.0.0", "/c", 2048) };

        // Act
        _sut.ReportCachedPlugins("/root", cached);

        // Assert
        var evt = StdoutEvents().ShouldHaveSingleItem();
        evt.GetProperty("cacheRoot").GetString().ShouldBe("/root");
        evt.GetProperty("plugins")[0].GetProperty("sizeBytes").GetInt64().ShouldBe(2048);
    }

    [Fact]
    public void ReportPluginDetail_EmitsSingleObject_OmittingNullCachePath()
    {
        // Act
        _sut.ReportPluginDetail(new ProjectPlugin("backend", "s3", "NSchema.Aws", "4.0.0", Restored: false, CachePath: null));

        // Assert
        var evt = StdoutEvents().ShouldHaveSingleItem();
        evt.GetProperty("label").GetString().ShouldBe("s3");
        evt.GetProperty("restored").GetBoolean().ShouldBeFalse();
        evt.TryGetProperty("cachePath", out _).ShouldBeFalse();
    }

    [Fact]
    public void Output_IsNewlineDelimited_OneObjectPerLine()
    {
        _sut.ReportDiff(new DatabaseDiff());
        _sut.ReportSchema(new DatabaseSchema());

        StdoutEvents().Select(e => e.GetProperty("type").GetString()).ShouldBe(["diff", "schema"]);
    }
}
