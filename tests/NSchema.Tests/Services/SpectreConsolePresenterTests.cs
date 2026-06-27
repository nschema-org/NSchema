using NSchema.Configuration.Plugins;
using NSchema.Diff;
using NSchema.Diff.Model;
using NSchema.Operations;
using NSchema.Plan.Model;
using NSchema.Policies;
using NSchema.Schema;
using NSchema.Schema.Model;
using NSchema.Schema.Model.Scripts;
using NSchema.Services;
using NSchema.Sql;
using NSchema.Sql.Model;
using NSchema.State.Model;
using Spectre.Console.Testing;

namespace NSchema.Tests.Services;

public sealed class SpectreConsolePresenterTests
{
    private readonly TestConsole _out = new();
    private readonly TestConsole _error = new();
    private readonly IDiffRenderer _diffRenderer = Substitute.For<IDiffRenderer>();
    private readonly ISchemaRenderer _schemaRenderer = Substitute.For<ISchemaRenderer>();
    private readonly ISqlPlanRenderer _sqlPlanRenderer = Substitute.For<ISqlPlanRenderer>();
    private readonly SpectreConsolePresenter _sut;

    public SpectreConsolePresenterTests()
    {
        _out.Profile.Width = 200;
        _error.Profile.Width = 200;
        _sut = Build(Verbosity.Normal);
    }

    private SpectreConsolePresenter Build(Verbosity verbosity) =>
        new(_out, _error, _diffRenderer, _schemaRenderer, _sqlPlanRenderer, verbosity);

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
    public void Error_WritesMessageToErrorConsole()
    {
        // Exception
        var ex = new Exception("Boom!");

        // Act
        _sut.ReportException(ex);

        // Assert
        _error.Output.ShouldContain("Boom!");
        _out.Output.ShouldBeEmpty();
    }

    [Fact]
    public void ReportDiff_FramesTheRenderedDiffInAPanel()
    {
        // Arrange
        _diffRenderer.Render(Arg.Any<DatabaseDiff>()).Returns(
            "+ table app.widgets\n    + id bigint not null\nPlan: 1 to add, 0 to change, 0 to destroy.");

        // Act
        _sut.ReportDiff(new DatabaseDiff());

        // Assert
        _out.Output.ShouldContain("Plan");
        _out.Output.ShouldContain("table app.widgets");
        _out.Output.ShouldContain("1 to add");
    }

    [Fact]
    public void ReportDiff_DoesNotThrow_WhenRenderedTextContainsMarkupCharacters()
    {
        // Arrange — column array types contain square brackets, which are Spectre markup delimiters.
        _diffRenderer.Render(Arg.Any<DatabaseDiff>()).Returns("~ tags type: text → text[]");

        // Act
        _sut.ReportDiff(new DatabaseDiff());

        // Assert
        _out.Output.ShouldContain("text[]");
    }

    [Fact]
    public void ReportSchema_FramesTheRenderedSchemaInASection()
    {
        // Arrange
        _schemaRenderer.Render(Arg.Any<DatabaseSchema>()).Returns("table app.widgets\n    id bigint not null");

        // Act
        _sut.ReportSchema(schema: null!);

        // Assert
        _out.Output.ShouldContain("Schema");
        _out.Output.ShouldContain("table app.widgets");
    }

    [Fact]
    public void ReportSchema_DoesNotThrow_WhenRenderedTextContainsMarkupCharacters()
    {
        // Arrange — column array types contain square brackets, which are Spectre markup delimiters.
        _schemaRenderer.Render(Arg.Any<DatabaseSchema>()).Returns("tags text[]");

        // Act
        _sut.ReportSchema(schema: null!);

        // Assert
        _out.Output.ShouldContain("text[]");
    }

    [Fact]
    public void ReportSqlPlan_FramesTheRenderedSqlInAPanel()
    {
        // Arrange
        _sqlPlanRenderer.Render(Arg.Any<SqlPlan>()).Returns("SQL Preview:\n-- [1/1]\nCREATE TABLE app.widgets ();");

        // Act
        _sut.ReportSqlPlan(new SqlPlan([]));

        // Assert
        _out.Output.ShouldContain("SQL");
        _out.Output.ShouldContain("CREATE TABLE app.widgets");
    }

    [Fact]
    public void ReportPlan_ListsEachScriptNameUnderItsSection()
    {
        // Arrange
        var plan = new MigrationPlan(
            [],
            [new Script("seed-roles", "INSERT INTO app.roles VALUES ('admin');", ScriptType.PreDeployment)],
            [new Script("reindex", "REINDEX TABLE app.widgets;", ScriptType.PostDeployment)]);

        // Act
        _sut.ReportPlan(plan);

        // Assert
        _out.Output.ShouldContain("Pre-deployment");
        _out.Output.ShouldContain("seed-roles");
        _out.Output.ShouldContain("Post-deployment");
        _out.Output.ShouldContain("reindex");
    }

    [Fact]
    public void ReportPlan_SkipsSectionsWithNoScripts()
    {
        // Arrange
        var plan = new MigrationPlan(
            [],
            [],
            [new Script("reindex", "REINDEX TABLE app.widgets;", ScriptType.PostDeployment)]);

        // Act
        _sut.ReportPlan(plan);

        // Assert
        _out.Output.ShouldNotContain("Pre-deployment");
        _out.Output.ShouldContain("Post-deployment");
    }

    [Fact]
    public void ReportPlan_WritesNothing_WhenThereAreNoScripts()
    {
        // Arrange
        var plan = new MigrationPlan([], [], []);

        // Act
        _sut.ReportPlan(plan);

        // Assert
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
        var diagnostics = new PolicyDiagnostics([new PolicyDiagnostic("style", "Naming hint", PolicyDiagnosticSeverity.Info)]);

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
            new PolicyDiagnostic("destructive", "Dropping column id", PolicyDiagnosticSeverity.Error),
        ]);

        // Act
        _sut.ReportDiagnostics(diagnostics);

        // Assert
        _error.Output.ShouldContain("Dropping column id");
        _error.Output.ShouldContain("destructive");
        _out.Output.ShouldBeEmpty();
    }
}
