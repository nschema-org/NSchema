using NSchema.Diff;
using NSchema.Diff.Model;
using NSchema.Operations;
using NSchema.Plan.Model;
using NSchema.Policies;
using NSchema.Schema;
using NSchema.Schema.Model;
using NSchema.Scripts.Model;
using NSchema.Services;
using NSchema.Sql;
using NSchema.Sql.Model;
using Spectre.Console.Testing;

namespace NSchema.Tests.Services;

public sealed class SpectreOperationReporterTests
{
    private readonly TestConsole _out = new();
    private readonly TestConsole _error = new();
    private readonly IDiffRenderer _diffRenderer = Substitute.For<IDiffRenderer>();
    private readonly ISchemaRenderer _schemaRenderer = Substitute.For<ISchemaRenderer>();
    private readonly ISqlPlanRenderer _sqlPlanRenderer = Substitute.For<ISqlPlanRenderer>();
    private readonly SpectreOperationReporter _sut;

    public SpectreOperationReporterTests()
    {
        _out.Profile.Width = 200;
        _error.Profile.Width = 200;
        _sut = new SpectreOperationReporter(_out, _error, _diffRenderer, _schemaRenderer, _sqlPlanRenderer);
    }

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
    public void Report_DoesNotThrow_WhenMessageContainsMarkupCharacters()
    {
        // Arrange — object names with array types contain square brackets, which are Spectre markup delimiters.
        _sut.Report(MessageKind.Success, "Imported app.events [text[]].");

        // Assert
        _out.Output.ShouldContain("app.events [text[]].");
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
        _sut.ReportDiff(diff: null!);

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
        _sut.ReportDiff(diff: null!);

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
