using NSchema.Diff;
using NSchema.Diff.Model;
using NSchema.Plan.Model;
using NSchema.Plan.PlanFile;
using NSchema.Schema;
using NSchema.Schema.Model;
using NSchema.Schema.Model.Scripts;
using NSchema.Services.Reporting;
using NSchema.Sql;
using NSchema.Sql.Model;
using Spectre.Console.Testing;

namespace NSchema.Tests.Services;

public sealed class SpectreConsolePresenterTests
{
    private readonly TestConsole _out = new();
    private readonly IDiffRenderer _diffRenderer = Substitute.For<IDiffRenderer>();
    private readonly ISchemaRenderer _schemaRenderer = Substitute.For<ISchemaRenderer>();
    private readonly ISqlPlanRenderer _sqlPlanRenderer = Substitute.For<ISqlPlanRenderer>();
    private readonly SpectreConsolePresenter _sut;

    public SpectreConsolePresenterTests()
    {
        _out.Profile.Width = 200;
        _sut = new SpectreConsolePresenter(_out, _diffRenderer, _schemaRenderer, _sqlPlanRenderer);
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
    public void ReportSavedPlan_RendersDiffScriptsAndSqlSections()
    {
        // Arrange — humans still get all three sections; only --json collapses to a single object.
        _diffRenderer.Render(Arg.Any<DatabaseDiff>()).Returns("+ table app.widgets");
        _sqlPlanRenderer.Render(Arg.Any<SqlPlan>()).Returns("CREATE TABLE app.widgets ();");
        var envelope = new PlanFileEnvelope(
            new DatabaseDiff(),
            new MigrationPlan([], [new Script("seed-roles", "INSERT INTO app.roles VALUES ('admin');", ScriptType.PreDeployment)], []),
            new SqlPlan([]),
            CreatedAt: default);

        // Act
        _sut.ReportSavedPlan(envelope);

        // Assert
        _out.Output.ShouldContain("Plan");
        _out.Output.ShouldContain("table app.widgets");
        _out.Output.ShouldContain("Pre-deployment");
        _out.Output.ShouldContain("seed-roles");
        _out.Output.ShouldContain("SQL");
        _out.Output.ShouldContain("CREATE TABLE app.widgets");
    }
}
