using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSchema.Diff.Domain;
using NSchema.Diff.Domain.Services;
using NSchema.Diff.Plugins;
using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Schemas;
using NSchema.Model.Scripts;
using NSchema.Model.Tables;
using NSchema.Plan.Policies;
using NSchema.Project.Domain.Directives;
using NSchema.State.Domain;
using DatabaseComparer = NSchema.Diff.Domain.Services.DatabaseComparer;

namespace NSchema.Tests.Diff;

/// <summary>
/// The complete current→desired difference: run-once resolution and change-event matching compose with the
/// structural compare, so the diff carries the script runs it implies.
/// </summary>
public sealed class ProjectComparerTests
{
    private static readonly Database _emptySchema = new Database { Schemas = [] };
    private static readonly DatabaseDiff _emptyDiff = new([]);

    private ProjectComparer Sut => new(new DatabaseComparer(NullLogger<DatabaseComparer>.Instance, new SqlEquivalence()));

    /// <summary>Current <c>app.users(id)</c> — the table a column-add migration targets.</summary>
    private static CurrentState UsersWithId() => new(new Database
    {
        Schemas = [new Schema { Name = "app",
        Tables = [new Table { Name = "users", Columns = [new Column { Name = "id", Type = SqlType.Int }] }] }],
    });

    /// <summary>Desired <c>app.users(id, email)</c> — adds the column the migration accompanies.</summary>
    private static Database UsersWithEmail(bool required = false) => new Database
    {
        Schemas = [new Schema { Name = "app",
        Tables = [new Table { Name = "users", Columns = [new Column { Name = "id", Type = SqlType.Int }, new Column { Name = "email", Type = SqlType.Text, IsNullable = !required }] }] }],
    };

    private static DeploymentScript SeedScript() =>
        new DeploymentScript("seed", "INSERT INTO app.c VALUES (1);", null, DeploymentPhase.Post) { RunCondition = RunCondition.Once };

    private static ChangeScript EmailBackfillMigration() =>
        new ChangeScript("backfill_emails", "UPDATE app.users SET email = ''", new ChangeTarget("app", "users", "email", ChangeTrigger.AddColumn));

    /// <summary>A current state recording <paramref name="sql"/> as <paramref name="script"/>'s executed body.</summary>
    private static CurrentState Executed(Script script, string sql) =>
        new(_emptySchema, [new ScriptExecution(script.Address, (script with { Sql = sql }).Hash, DateTimeOffset.UnixEpoch)]);

    [Fact]
    public void Compare_DiffsBothSchemas()
    {
        // Arrange — a schema only in current is removed; one only in desired is added.
        var current = new Database { Schemas = [new Schema { Name = "gone" }] };
        var desired = new Database { Schemas = [new Schema { Name = "fresh" }] };

        // Act
        var diff = Sut.Compare(new CurrentState(current), new ProjectDefinition(desired)).Require();

        // Assert
        diff.Schemas.Select(x => (x.Name.Value, x.Kind)).ShouldBe(
            [("fresh", ChangeKind.Add), ("gone", ChangeKind.Remove)], ignoreOrder: true);
    }

    [Fact]
    public void Compare_PendingDeploymentScript_LandsInTheDiff()
    {
        // Act — nothing recorded, so the script is part of the current→desired difference.
        var comparison = Sut.Compare(new CurrentState(_emptySchema), TestProjects.Project(_emptySchema, [SeedScript()]));

        // Assert
        comparison.Require().AllScripts().ShouldHaveSingleItem().Name.ShouldBe("seed");
        comparison.Require().IsEmpty.ShouldBeFalse();
        comparison.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Compare_ExecutedRunOnceScript_IsNotPartOfTheDifference()
    {
        // Act — the script has already run, so it is not part of the current→desired difference.
        var comparison = Sut.Compare(Executed(SeedScript(), SeedScript().Sql.Value), TestProjects.Project(_emptySchema, [SeedScript()]));

        // Assert
        comparison.Require().AllScripts().ShouldBeEmpty();
        comparison.Require().IsEmpty.ShouldBeTrue();
        comparison.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Compare_ExecutedRunOnceScriptWithChangedBody_StaysSkippedWithAWarning()
    {
        // Act — the recorded hash is of a different body; silently re-running is never safe.
        var comparison = Sut.Compare(Executed(SeedScript(), "some other body"), TestProjects.Project(_emptySchema, [SeedScript()]));

        // Assert
        comparison.Require().AllScripts().ShouldBeEmpty();
        var diagnostic = comparison.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Warning);
        diagnostic.Message.ShouldContain("'seed' has changed since it was executed");
    }

    [Fact]
    public void Compare_RunOnceExecutedUnderAnotherScope_StaysPending()
    {
        // Arrange — the ledger records (scope, name): an execution recorded for one schema's script does not
        // satisfy a global (or differently scoped) script sharing the name.
        var script = SeedScript();
        var scoped = new CurrentState(_emptySchema,
            [new ScriptExecution(new ScopedAddress("sales", script.Name), script.Hash, DateTimeOffset.UnixEpoch)]);

        // Act
        var comparison = Sut.Compare(scoped, TestProjects.Project(_emptySchema, [script]));

        // Assert
        comparison.Require().AllScripts().ShouldHaveSingleItem().ShouldBe(script);
        comparison.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Compare_AlwaysScript_IgnoresRecordedExecutions()
    {
        // Arrange — same name recorded, but the script is not run-once.
        var script = SeedScript() with { RunCondition = RunCondition.Always };

        // Act
        var comparison = Sut.Compare(Executed(script, script.Sql.Value), TestProjects.Project(_emptySchema, [script]));

        // Assert
        comparison.Require().AllScripts().ShouldHaveSingleItem();
        comparison.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Compare_MatchedMigration_RidesTheChangeItAccompanies()
    {
        // Arrange
        var migration = EmailBackfillMigration();

        // Act
        var comparison = Sut.Compare(UsersWithId(), TestProjects.Project(UsersWithEmail(), [migration]));

        // Assert — the added column carries the script inline; AllScripts walks it up.
        comparison.Require().Schemas[0].Tables[0].Columns.Single(c => c.Name.Value == "email").MigrationScript.ShouldBe(migration);
        comparison.Require().AllScripts().ShouldBe([migration]);
        comparison.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Compare_UnmatchedMigration_StaysOutOfTheDiff_WithADeadBlockDiagnostic()
    {
        // Arrange — the schema does not change, so the declared migration matches nothing.
        var migration = EmailBackfillMigration();

        // Act
        var comparison = Sut.Compare(new CurrentState(_emptySchema), TestProjects.Project(_emptySchema, [migration]));

        // Assert
        comparison.Require().AllScripts().ShouldBeEmpty();
        var diagnostic = comparison.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Source.ShouldBe("data-migrations");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Info);
        diagnostic.Message.ShouldBe(
            "Migration 'app.backfill_emails' (ADD COLUMN app.users.email) matches no change in this plan and will " +
            "not run. If the change it supports has been applied everywhere, the block is safe to delete.");
    }

    [Fact]
    public void Compare_ChangeMigration_IgnoresRecordedExecutions()
    {
        // Arrange — a matching execution is on record, but a change-event script has no ledger: its run is
        // gated by the change alone, so a re-planned change re-runs it.
        var migration = EmailBackfillMigration();
        var current = new CurrentState(UsersWithId().Database,
            [new ScriptExecution(migration.Address, migration.Hash, DateTimeOffset.UnixEpoch)]);

        // Act
        var comparison = Sut.Compare(current, TestProjects.Project(UsersWithEmail(), [migration]));

        // Assert — the change lands and the migration rides it, the recorded execution notwithstanding.
        comparison.Require().Schemas[0].Tables[0].Columns.Single(c => c.Name.Value == "email").MigrationScript.ShouldBe(migration);
        comparison.Require().AllScripts().ShouldHaveSingleItem().ShouldBe(migration);
    }

    [Fact]
    public void Compare_MatchedBackfill_SuppressesTheDataHazardWarning()
    {
        // Arrange — the hazard policy sees the complete diff, so a matched backfill silences the NOT-NULL-add
        // warning it would otherwise raise.
        var policy = new DataHazardPolicy(Options.Create(new DataHazardOptions()));

        // Act — the same required-column add, once without a backfill (hazard) and once with (suppressed).
        var unmatched = Sut.Compare(UsersWithId(), new ProjectDefinition(UsersWithEmail(required: true)));
        var matched = Sut.Compare(UsersWithId(), TestProjects.Project(UsersWithEmail(required: true), [EmailBackfillMigration()]));

        // Assert
        policy.Validate(unmatched.Require()).ShouldHaveSingleItem().Source.ShouldBe("data-hazards");
        policy.Validate(matched.Require()).ShouldBeEmpty();
    }

    [Fact]
    public void Compare_AgainstAnEmptyProject_RemovesEverything_WithNoScripts()
    {
        // Arrange — a teardown is not a third kind of compare: it is the recorded schema against nothing.
        var current = new CurrentState(new Database { Schemas = [new Schema { Name = "app" }] });

        // Act
        var diff = Sut.Compare(current, new ProjectDefinition(new Database())).Require();

        // Assert
        diff.Schemas.ShouldHaveSingleItem().Kind.ShouldBe(ChangeKind.Remove);
        diff.AllScripts().ShouldBeEmpty();
    }

    [Fact]
    public void Compare_AppliedRename_ReportsTheSpentDirective()
    {
        // Arrange — current has 'people' and no 'users': the rename has demonstrably been applied here.
        var current = new CurrentState(new Database
        {
            Schemas = [new Schema { Name = "app",
            Tables = [new Table { Name = "people" }] }],
        });
        var directives = new ProjectDirectives(
            ObjectRenames: [new ObjectRenameDirective(new ObjectAddress("app", "users") with { Kind = ObjectKind.Table }, "people")]);

        // Act
        var comparison = Sut.Compare(current, new ProjectDefinition(_emptySchema, directives));

        // Assert
        var diagnostic = comparison.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Info);
        diagnostic.Source.ShouldBe("directives");
        diagnostic.Message.ShouldContain("Rename of table 'app.users'");
        diagnostic.Message.ShouldContain("safe to delete");
    }

    [Fact]
    public void Compare_RenameOnAFreshDatabase_StaysSilent()
    {
        // Arrange — neither side of the rename exists (a fresh environment): the directive is pending, not
        // spent, so no expiry info fires.
        var directives = new ProjectDirectives(
            ObjectRenames: [new ObjectRenameDirective(new ObjectAddress("app", "users") with { Kind = ObjectKind.Table }, "people")]);

        // Act
        var comparison = Sut.Compare(new CurrentState(_emptySchema), new ProjectDefinition(_emptySchema, directives));

        // Assert
        comparison.Diagnostics.ShouldBeEmpty();
    }
}
