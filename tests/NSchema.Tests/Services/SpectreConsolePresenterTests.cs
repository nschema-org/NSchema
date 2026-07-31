using NSchema.Diff.Domain;
using NSchema.Diff.Domain.Columns;
using NSchema.Diff.Domain.Schemas;
using NSchema.Diff.Domain.Tables;
using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Schemas;
using NSchema.Model.Scripts;
using NSchema.Model.Tables;
using NSchema.Plan.Domain;
using NSchema.Services.Reporting;
using NSchema.State.Domain;
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
        SchemaDiff.Added("app") with
        {
            Tables =
            [
                TableDiff.Modified("app", "widgets") with
                {
                    Columns =
                    [
                        ColumnDiff.Modified(new Column { Name = "tags", Type = new SqlType("text[]") })
                            with { Type = new ValueChange<SqlType>(new SqlType("text"), new SqlType("text[]")) },
                    ],
                },
            ],
        },
    ]);

    [Fact]
    public void ReportDiff_FramesTheRenderedDiffInAPanel()
    {
        // Arrange
        var diff = new DatabaseDiff([SchemaDiff.Added("app")]);

        // Act
        _sut.ReportDiff(diff);

        // Assert
        _out.Output.ShouldContain("Plan");
        _out.Output.ShouldContain("+ schema app"); // the add glyph comes from the line's change kind, not parsed text
        _out.Output.ShouldContain("1 to add");
    }

    [Fact]
    public void ReportDiff_TouchedSchema_RendersItsContentsWithoutASchemaHeader()
    {
        // Arrange — 'app' is Touched: in the diff only because a table inside it changed. It is not itself being
        // created, altered or dropped, so announcing it would read as a change the plan is not making.
        var diff = new DatabaseDiff(
        [
            SchemaDiff.Containing("app") with
            {
                Tables =
                [
                    TableDiff.Modified("app", "orders") with
                    {
                        Columns = [ColumnDiff.Added(new Column { Name = "placed_at", Type = SqlType.BigInt })],
                    },
                ],
            },
        ]);

        // Act
        _sut.ReportDiff(diff);

        // Assert — the contents render, the schema itself is not announced, and nothing falls through to the
        // unknown-change glyph.
        _out.Output.ShouldContain("table app.orders");
        _out.Output.ShouldContain("placed_at");
        _out.Output.ShouldNotContain("schema app");
        _out.Output.ShouldNotContain("?");
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
    public void ReportDiff_ListsTheDiffsDeploymentScripts()
    {
        // Arrange — the scripts ride the diff now, so the plan section carries them.
        var diff = new DatabaseDiff([SchemaDiff.Added("app")])
        {
            DeploymentScripts =
            [
                new DeploymentScript("seed-roles", "INSERT INTO app.roles VALUES ('admin');", ScopeSchema: null, DeploymentPhase.Pre),
                new DeploymentScript("reindex", "REINDEX TABLE app.widgets;", ScopeSchema: null, DeploymentPhase.Post),
            ],
        };

        // Act
        _sut.ReportDiff(diff);

        // Assert
        _out.Output.ShouldContain("script seed-roles (on pre deployment)");
        _out.Output.ShouldContain("script reindex (on post deployment)");
    }

    [Fact]
    public void ReportPlan_Adoptions_AreListedAndCounted()
    {
        // Arrange — nothing differs, so taking the objects over is all the apply would do.
        var plan = new MigrationPlan(new DatabaseDiff(), [])
        {
            Adopted = new IdentitySet(
                DatabaseObjects: [DatabaseAddress.Schema("app")],
                SchemaObjects: [ObjectAddress.Table("app", "users")]),
        };

        // Act
        _sut.ReportPlan(plan);

        // Assert — an apply is not a no-op here, so the section must not read as one.
        _out.Output.ShouldContain("Adopting 2 existing objects into management:");
        _out.Output.ShouldContain("= app.users");
        _out.Output.ShouldContain("2 to adopt");
        _out.Output.ShouldNotContain("No changes detected");
    }

    [Fact]
    public void ReportPlan_WithoutAdoptions_ReportsOnlyTheDifference()
    {
        // Act
        _sut.ReportPlan(new MigrationPlan(new DatabaseDiff([SchemaDiff.Added("app")]), []));

        // Assert
        _out.Output.ShouldContain("+ schema app");
        _out.Output.ShouldNotContain("adopt");
    }

    [Fact]
    public void ReportDatabase_FramesTheRenderedSchemaInASection()
    {
        // Arrange
        var database = new Database
        {
            Schemas =
            [
                new Schema
                {
                    Name = "app",
                    Tables = [new Table { Name = "widgets", Columns = [new Column { Name = "id", Type = SqlType.BigInt }] }],
                },
            ],
        };

        // Act
        _sut.ReportDatabase(database);

        // Assert — the section is titled for what it holds, as the Markdown face titles it.
        _out.Output.ShouldContain("Database");
        _out.Output.ShouldContain("table widgets");
    }

    // The ANSI escape for [dim], which a console emitting its styling writes ahead of the dimmed text.
    private const string Dim = "\u001b[2m";

    /// <summary>Recorded state whose schema holds one managed table and one NSchema knows nothing about.</summary>
    private static DatabaseState PartlyManagedState()
    {
        var database = new Database
        {
            Schemas =
            [
                new Schema
                {
                    Name = "app",
                    Tables =
                    [
                        new Table { Name = "users", Columns = [new Column { Name = "id", Type = SqlType.BigInt }] },
                        new Table { Name = "legacy_audit", Columns = [new Column { Name = "archived_at", Type = SqlType.Text }] },
                    ],
                },
            ],
        };

        return new DatabaseState(database, [])
        {
            Managed = new IdentitySet(
                DatabaseObjects: [DatabaseAddress.Schema("app")],
                SchemaObjects: [ObjectAddress.Table("app", "users")]),
        };
    }

    [Fact]
    public void ReportState_MarksWhatIsNotManaged()
    {
        // Act
        _sut.ReportState(PartlyManagedState());

        // Assert — the marker survives markup escaping.
        _out.Output.ShouldContain("table legacy_audit [unmanaged]");
        _out.Output.ShouldContain("Managed: 2 of 3 recorded objects.");
    }

    [Fact]
    public void ReportState_ReportsTheLedgerBesideTheSchema()
    {
        // Arrange — recorded state is the schema plus what has run against it, so both sections are the report.
        var state = PartlyManagedState() with
        {
            Scripts = [new ScriptExecution(new ScriptReference(null, "seed-users"), new ScriptHash("abc123"), DateTimeOffset.UnixEpoch)],
        };

        // Act
        _sut.ReportState(state);

        // Assert
        _out.Output.ShouldContain("Scripts");
        _out.Output.ShouldContain("seed-users");
        _out.Output.ShouldContain("abc123");
    }

    [Fact]
    public void ReportState_NothingRecorded_StillReportsTheLedgerSection()
    {
        // Act — an omitted section would read as "no ledger here" rather than "nothing has run".
        _sut.ReportState(PartlyManagedState());

        // Assert
        _out.Output.ShouldContain("No script executions are recorded");
    }

    [Fact]
    public void ReportState_DimsTheWholeUnmanagedBlock()
    {
        // Arrange — a console that emits its styling, so what is dimmed can be asserted rather than inferred.
        var console = new TestConsole().EmitAnsiSequences();
        console.Profile.Width = 200;
        var sut = new SpectreConsolePresenter(console);

        // Act
        sut.ReportState(PartlyManagedState());

        // Assert — the marked object and the columns beneath it are dimmed; what NSchema manages is not.
        var lines = console.Output.Split('\n');
        lines.Single(line => line.Contains("table legacy_audit")).ShouldContain(Dim);
        lines.Single(line => line.Contains("archived_at")).ShouldContain(Dim);
        lines.Single(line => line.Contains("table users")).ShouldNotContain(Dim);
        lines.Single(line => line.Contains("id bigint")).ShouldNotContain(Dim);
        lines.Single(line => line.Contains("Managed:")).ShouldNotContain(Dim);
    }

    [Fact]
    public void ReportScripts_WritesTheLedgerAsATable()
    {
        // Act
        _sut.ReportScripts([new ScriptExecution(new ScriptReference(null, "seed-users"), new ScriptHash("abc123"), DateTimeOffset.UnixEpoch)]);

        // Assert — the ledger's data as a table: name, execution time, and body hash.
        _out.Output.ShouldContain("seed-users");
        _out.Output.ShouldContain("1970-01-01");
        _out.Output.ShouldContain("abc123");
    }

    [Fact]
    public void ReportScripts_ScopedScript_NamesItByItsReference()
    {
        // Act
        _sut.ReportScripts([new ScriptExecution(new ScriptReference("app", "backfill"), new ScriptHash("abc123"), DateTimeOffset.UnixEpoch)]);

        // Assert — the same `schema.name` spelling `script taint` takes.
        _out.Output.ShouldContain("app.backfill");
    }

    [Fact]
    public void ReportScripts_NothingRecorded_SaysSo()
    {
        // Act — an empty table would read as a rendering failure rather than an empty ledger.
        _sut.ReportScripts([]);

        // Assert
        _out.Output.ShouldContain("No script executions are recorded");
    }

    [Fact]
    public void ReportDatabase_DoesNotThrow_WhenRenderedTextContainsMarkupCharacters()
    {
        // Arrange — a column whose type is an array renders `text[]`, exercising markup escaping.
        var database = new Database
        {
            Schemas =
            [
                new Schema
                {
                    Name = "app",
                    Tables = [new Table { Name = "widgets", Columns = [new Column { Name = "tags", Type = new SqlType("text[]") }] }],
                },
            ],
        };

        // Act
        _sut.ReportDatabase(database);

        // Assert
        _out.Output.ShouldContain("text[]");
    }

    [Fact]
    public void ReportPlan_FramesTheRenderedSqlInItsOwnSection()
    {
        // Arrange
        var plan = new MigrationPlan(new DatabaseDiff([SchemaDiff.Added("app")]),
            [new SqlStatement("CREATE TABLE app.widgets ();", RunOutsideTransaction: false)]);

        // Act
        _sut.ReportPlan(plan);

        // Assert
        _out.Output.ShouldContain("SQL");
        _out.Output.ShouldContain("CREATE TABLE app.widgets");
    }

    [Fact]
    public void ReportPlan_NumbersStatementsAndFlagsTheOnesOutsideATransaction()
    {
        // Arrange
        var plan = new MigrationPlan(new DatabaseDiff([SchemaDiff.Added("app")]),
        [
            new SqlStatement("CREATE INDEX CONCURRENTLY ix ON app.widgets (id)", RunOutsideTransaction: true),
            new SqlStatement("ANALYZE app.widgets", RunOutsideTransaction: false),
        ]);

        // Act
        _sut.ReportPlan(plan);

        // Assert — headers number each statement; only the concurrent one is flagged, read from the model.
        var output = _out.Output;
        output.ShouldContain("-- [1/2] (outside transaction)");
        output.ShouldContain("-- [2/2]");
        output.ShouldNotContain("-- [2/2] (outside transaction)");
        output.IndexOf("CREATE INDEX").ShouldBeLessThan(output.IndexOf("ANALYZE"));
    }

    [Fact]
    public void ReportPlan_NoStatements_WritesNoSqlSection()
    {
        // Act — an apply that executes nothing has no SQL to preview; the plan section says what it does instead.
        _sut.ReportPlan(new MigrationPlan(new DatabaseDiff(), []));

        // Assert
        _out.Output.ShouldContain("No changes detected");
        _out.Output.ShouldNotContain("SQL");
    }
}
