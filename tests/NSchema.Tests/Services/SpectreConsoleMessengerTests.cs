using NSchema.Configuration.Plugins;
using NSchema.Diagnostics;
using NSchema.Policies;
using NSchema.Services.Reporting;
using NSchema.State.Model;
using Spectre.Console.Testing;

namespace NSchema.Tests.Services;

public sealed class SpectreConsoleMessengerTests
{
    private readonly TestConsole _out = new();
    private readonly TestConsole _error = new();
    private readonly SpectreConsoleMessenger _sut;

    public SpectreConsoleMessengerTests()
    {
        _out.Profile.Width = 200;
        _error.Profile.Width = 200;
        _sut = Build(Verbosity.Normal);
    }

    private SpectreConsoleMessenger Build(Verbosity verbosity) => new(_out, _error, verbosity);

    [Theory]
    [InlineData(MessageKind.Announcement)]
    [InlineData(MessageKind.Progress)]
    [InlineData(MessageKind.Success)]
    public void Report_WritesNonWarningMessagesToOutput(MessageKind kind)
    {
        // Act
        _sut.Report(kind, "Planning complete.");

        // Assert
        _out.Output.ShouldContain("Planning complete.");
        _error.Output.ShouldBeEmpty();
    }

    [Fact]
    public void Report_RoutesWarningsToErrorConsole()
    {
        // Act
        _sut.Report(MessageKind.Warning, "State store is stale.");

        // Assert — warnings go to stderr, matching the diagnostics routing.
        _error.Output.ShouldContain("State store is stale.");
        _out.Output.ShouldBeEmpty();
    }

    [Fact]
    public void Report_NormalVerbosity_SuppressesVerboseDetail()
    {
        _sut.Report(MessageKind.Verbose, "Read 2 DDL files.");

        _out.Output.ShouldBeEmpty();
        _error.Output.ShouldBeEmpty();
    }

    [Fact]
    public void Report_VerboseVerbosity_ShowsVerboseDetail()
    {
        Build(Verbosity.Verbose).Report(MessageKind.Verbose, "Read 2 DDL files.");

        _out.Output.ShouldContain("Read 2 DDL files.");
    }

    [Theory]
    [InlineData(MessageKind.Verbose)]
    [InlineData(MessageKind.Announcement)]
    [InlineData(MessageKind.Progress)]
    public void Report_QuietVerbosity_SuppressesNarration(MessageKind kind)
    {
        Build(Verbosity.Quiet).Report(kind, "chatter");

        _out.Output.ShouldBeEmpty();
        _error.Output.ShouldBeEmpty();
    }

    [Fact]
    public void Report_QuietVerbosity_StillShowsOutcomesAndWarnings()
    {
        var quiet = Build(Verbosity.Quiet);

        quiet.Report(MessageKind.Success, "Apply complete.");
        quiet.Report(MessageKind.Warning, "Drift detected.");

        _out.Output.ShouldContain("Apply complete.");
        _error.Output.ShouldContain("Drift detected.");
    }

    [Fact]
    public void Report_DoesNotThrow_WhenMessageContainsMarkupCharacters()
    {
        // Arrange — object names with array types contain square brackets, which are Spectre markup delimiters.
        _sut.Report(MessageKind.Success, "Imported app.events [text[]].");

        // Assert
        _out.Output.ShouldContain("app.events [text[]].");
    }

    [Fact]
    public void HighlightedHole_WithMarkupCharacters_IsEscaped()
    {
        // Arrange — array types contain square brackets, which are Spectre markup delimiters.
        var name = "app.events [text[]]";

        // Act
        _sut.Success($"Imported {name}");

        // Assert — escaped, not interpreted or leaked.
        _out.Output.ShouldContain("app.events [text[]]");
    }

    [Fact]
    public void ReportLockInfo_Null_WritesNothing()
    {
        // The absence of a lock has no data to render; the "not locked" narrative is the command's.
        _sut.ReportLockInfo(null);

        _out.Output.ShouldBeEmpty();
        _error.Output.ShouldBeEmpty();
    }

    [Fact]
    public void ReportLockInfo_Held_WritesLockDetailLinesToOutput()
    {
        var info = new StateLockInfo("abc123", "apply", "tom@dev", DateTimeOffset.UnixEpoch);

        _sut.ReportLockInfo(info);

        // Just the lock's data, as detail lines on stdout — no headline narrative (that's the command's).
        _out.Output.ShouldContain("abc123");
        _out.Output.ShouldContain("tom@dev");
        _out.Output.ShouldContain("apply");
        _error.Output.ShouldBeEmpty();
    }

    [Fact]
    public void ReportScriptExecutions_Empty_WritesNoExecutionsMessage()
    {
        _sut.ReportScripts([]);

        _out.Output.ShouldContain("No run-once script executions are recorded");
        _error.Output.ShouldBeEmpty();
    }

    [Fact]
    public void ReportScriptExecutions_WritesTheLedgerTableToOutput()
    {
        _sut.ReportScripts([new ScriptRecord("seed-users", "abc123", DateTimeOffset.UnixEpoch)]);

        // The ledger's data as a table on stdout — name, execution time, and body hash.
        _out.Output.ShouldContain("seed-users");
        _out.Output.ShouldContain("1970-01-01");
        _out.Output.ShouldContain("abc123");
        _error.Output.ShouldBeEmpty();
    }

    [Fact]
    public void ReportProjectPlugins_Empty_WritesNoPluginsMessage()
    {
        // Act
        _sut.ReportProjectPlugins([]);

        // Assert
        _out.Output.ShouldContain("No provider or backend plugins");
    }

    [Fact]
    public void ReportProjectPlugins_WritesATableOfPlugins()
    {
        // Act
        _sut.ReportProjectPlugins([new ProjectPlugin("provider", "postgres", "NSchema.Postgres", "4.0.0", true, "/c")]);

        // Assert
        _out.Output.ShouldContain("postgres");
        _out.Output.ShouldContain("NSchema.Postgres");
        _out.Output.ShouldContain("4.0.0");
    }

    [Fact]
    public void ReportCachedPlugins_Empty_WritesEmptyMessageWithRoot()
    {
        // Act
        _sut.ReportCachedPlugins("/cache/root", []);

        // Assert
        _out.Output.ShouldContain("/cache/root");
        _out.Output.ShouldContain("empty");
    }

    [Fact]
    public void ReportCachedPlugins_WritesPackageVersionAndHumanReadableSize()
    {
        // Act — 2 MiB renders as a compact binary size.
        _sut.ReportCachedPlugins("/cache/root", [new CachedPlugin("NSchema.Postgres", "4.0.0", "/c", 2 * 1024 * 1024)]);

        // Assert
        _out.Output.ShouldContain("NSchema.Postgres");
        _out.Output.ShouldContain("4.0.0");
        _out.Output.ShouldContain("MiB");
    }

    [Fact]
    public void ReportPluginDetail_NotRestored_HintsToRunInit()
    {
        // Act
        _sut.ReportPluginDetail(new ProjectPlugin("backend", "s3", "NSchema.Aws", "4.0.0", false, null));

        // Assert
        _out.Output.ShouldContain("s3");
        _out.Output.ShouldContain("NSchema.Aws");
        _out.Output.ShouldContain("init");
    }

    [Fact]
    public void ReportException_WritesMessageToErrorConsole()
    {
        // Arrange
        var ex = new Exception("Boom!");

        // Act
        _sut.ReportException(ex);

        // Assert
        _error.Output.ShouldContain("Boom!");
        _out.Output.ShouldBeEmpty();
    }

    [Fact]
    public void ReportDiagnostics_WritesPlaceholder_WhenEmpty()
    {
        // Act
        _sut.ReportDiagnostics(new PolicyDiagnostics());

        // Assert
        _out.Output.ShouldContain("No policy diagnostics.");
    }

    [Fact]
    public void ReportDiagnostics_WritesInfoToOutput()
    {
        // Arrange
        var diagnostics = new PolicyDiagnostics([new Diagnostic("style", "Naming hint", DiagnosticSeverity.Info)]);

        // Act
        _sut.ReportDiagnostics(diagnostics);

        // Assert
        _out.Output.ShouldContain("Naming hint");
        _error.Output.ShouldBeEmpty();
    }

    [Fact]
    public void ReportDiagnostics_RoutesWarningsAndErrorsToErrorConsole()
    {
        // Arrange
        var diagnostics = new PolicyDiagnostics(
        [
            new Diagnostic("destructive", "Dropping column id", DiagnosticSeverity.Error),
        ]);

        // Act
        _sut.ReportDiagnostics(diagnostics);

        // Assert
        _error.Output.ShouldContain("Dropping column id");
        _error.Output.ShouldContain("destructive");
        _out.Output.ShouldBeEmpty();
    }
}
