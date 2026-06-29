using NSchema.Diff.Model;
using NSchema.Plan.Model;
using NSchema.Plan.PlanFile;
using NSchema.Schema.Model;
using NSchema.Schema.Model.Columns;
using NSchema.Schema.Model.Scripts;
using NSchema.Schema.Model.Schemas;
using NSchema.Schema.Model.Tables;
using NSchema.Services.Reporting;
using NSchema.Sql.Model;
using Spectre.Console.Testing;

namespace NSchema.Tests.Services;

public sealed class SpectreConsolePresenterTests
{
    private readonly TestConsole _out = new();
    private readonly SpectreConsolePresenter _sut;

    public SpectreConsolePresenterTests()
    {
        _out.Profile.Width = 200;
        _sut = new SpectreConsolePresenter(_out);
    }

    // A diff renaming nothing but adding one schema with a table whose column changes type to an array — the `[]`
    // exercises the presenter's markup escaping, and the added schema/table gives the framing tests real content.
    private static DatabaseDiff DiffWithArrayColumn() => new([
        new SchemaDiff("app", ChangeKind.Add, Tables:
        [
            new TableDiff("app", "widgets", ChangeKind.Modify, Columns:
            [
                new ColumnDiff("tags", ChangeKind.Modify, Type: new ValueChange<SqlType>(new SqlType("text"), new SqlType("text[]"))),
            ]),
        ]),
    ]);

    [Fact]
    public void ReportDiff_FramesTheRenderedDiffInAPanel()
    {
        // Arrange
        var diff = new DatabaseDiff([new SchemaDiff("app", ChangeKind.Add)]);

        // Act
        _sut.ReportDiff(diff);

        // Assert
        _out.Output.ShouldContain("Plan");
        _out.Output.ShouldContain("schema app");
        _out.Output.ShouldContain("1 to add");
    }

    [Fact]
    public void ReportDiff_DoesNotThrow_WhenRenderedTextContainsMarkupCharacters()
    {
        // Arrange — array types render as `text[]`, whose square brackets are Spectre markup delimiters.

        // Act
        _sut.ReportDiff(DiffWithArrayColumn());

        // Assert
        _out.Output.ShouldContain("text[]");
    }

    [Fact]
    public void ReportSchema_FramesTheRenderedSchemaInASection()
    {
        // Arrange
        var schema = new DatabaseSchema([
            new SchemaDefinition("app", Tables:
            [
                new Table("widgets", Columns: [new Column("id", SqlType.BigInt)]),
            ]),
        ]);

        // Act
        _sut.ReportSchema(schema);

        // Assert
        _out.Output.ShouldContain("Schema");
        _out.Output.ShouldContain("table widgets");
    }

    [Fact]
    public void ReportSchema_DoesNotThrow_WhenRenderedTextContainsMarkupCharacters()
    {
        // Arrange — a column whose type is an array renders `text[]`, exercising markup escaping.
        var schema = new DatabaseSchema([
            new SchemaDefinition("app", Tables:
            [
                new Table("widgets", Columns: [new Column("tags", new SqlType("text[]"))]),
            ]),
        ]);

        // Act
        _sut.ReportSchema(schema);

        // Assert
        _out.Output.ShouldContain("text[]");
    }

    [Fact]
    public void ReportSqlPlan_FramesTheRenderedSqlInAPanel()
    {
        // Arrange
        var plan = new SqlPlan([new SqlStatement("CREATE TABLE app.widgets ();", RunOutsideTransaction: false)]);

        // Act
        _sut.ReportSqlPlan(plan);

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
        var envelope = new PlanFileEnvelope(
            new DatabaseDiff([new SchemaDiff("app", ChangeKind.Add)]),
            new MigrationPlan([], [new Script("seed-roles", "INSERT INTO app.roles VALUES ('admin');", ScriptType.PreDeployment)], []),
            new SqlPlan([new SqlStatement("CREATE TABLE app.widgets ();", RunOutsideTransaction: false)]),
            CreatedAt: default);

        // Act
        _sut.ReportSavedPlan(envelope);

        // Assert
        _out.Output.ShouldContain("Plan");
        _out.Output.ShouldContain("schema app");
        _out.Output.ShouldContain("Pre-deployment");
        _out.Output.ShouldContain("seed-roles");
        _out.Output.ShouldContain("SQL");
        _out.Output.ShouldContain("CREATE TABLE app.widgets");
    }
}
