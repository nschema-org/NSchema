using System.Text.Json;
using NSchema.Configuration.Plugins;
using NSchema.Diagnostics;
using NSchema.Policies;
using NSchema.Services.Reporting;
using NSchema.State.Model;

namespace NSchema.Tests.Services;

public sealed class JsonConsoleMessengerTests
{
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();
    private readonly JsonConsoleMessenger _sut;

    public JsonConsoleMessengerTests() => _sut = Build(Verbosity.Normal);

    private JsonConsoleMessenger Build(Verbosity verbosity) => new(verbosity, _out, _error);

    private List<JsonElement> StderrEvents() => _error.ToString()
        .Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Select(line => JsonDocument.Parse(line).RootElement)
        .ToList();

    private List<JsonElement> StdoutEvents() => _out.ToString()
        .Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Select(line => JsonDocument.Parse(line).RootElement)
        .ToList();

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
    public void Report_QuietVerbosity_SuppressesProgress()
    {
        Build(Verbosity.Quiet).Report(MessageKind.Progress, "Loading desired schema...");

        _error.ToString().ShouldBeEmpty();
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
    public void ReportScriptExecutions_EmitsASingleArray()
    {
        // A query result: one clean array on stdout a script can consume directly.
        _sut.ReportScripts([new ScriptRecord("seed-users", "abc123", DateTimeOffset.UnixEpoch)]);

        var evt = StdoutEvents().ShouldHaveSingleItem();
        var record = evt.EnumerateArray().ShouldHaveSingleItem();
        record.GetProperty("name").GetString().ShouldBe("seed-users");
        record.GetProperty("hash").GetString().ShouldBe("abc123");
        record.GetProperty("executedUtc").GetDateTimeOffset().ShouldBe(DateTimeOffset.UnixEpoch);
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
    public void ReportDiagnostics_EmitsDiagnosticsEvent()
    {
        // Act
        _sut.ReportDiagnostics(new PolicyDiagnostics([new Diagnostic("destructive", "Dropping column id", DiagnosticSeverity.Error)]));

        // Assert
        var evt = StdoutEvents().ShouldHaveSingleItem();
        evt.GetProperty("type").GetString().ShouldBe("diagnostics");
        evt.GetProperty("diagnostics")[0].GetProperty("message").GetString().ShouldBe("Dropping column id");
    }

    [Fact]
    public void ReportDiagnostics_Empty_EmitsNothing()
    {
        _sut.ReportDiagnostics(new PolicyDiagnostics());

        _out.ToString().ShouldBeEmpty();
    }
}
