using System.Text;
using NSchema.Diff.Domain;
using NSchema.Diff.Domain.Columns;
using NSchema.Diff.Domain.Constraints;
using NSchema.Diff.Domain.Schemas;
using NSchema.Diff.Domain.Tables;
using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Scripts;
using NSchema.Model.Tables;
using NSchema.Plan.Domain;
using NSchema.Plan.PlanFile;

namespace NSchema.Tests.Plan.PlanFile;

public sealed class PlanFileManagerTests
{
    private static readonly PlanFileManager _sut = new();

    private static string Json(PlanFileEnvelope envelope) => Encoding.UTF8.GetString(_sut.Serialize(envelope).Span);

    private static PlanFileEnvelope SampleEnvelope()
    {
        // A plan carrying a rich diff (including a full Table definition), both script event kinds, and
        // statements with execution metadata, so the round-trip exercises the whole artifact.
        var backfill = new ChangeScript("backfill", "UPDATE app.users SET email = ''",
            new ChangeTarget("app", "users", "email", ChangeTrigger.AddColumn));
        var email = ColumnDiff.Added(new Column { Name = "email", Type = SqlType.Text }) with { MigrationScript = backfill };
        var key = PrimaryKeyDiff.Added(new PrimaryKey { Name = "users_pkey", ColumnNames = ["id"] });
        var users = TableDiff.Modified("app", "users") with
        {
            Columns = [email],
            PrimaryKeys = [key],
        };
        var plan = new MigrationPlan(
            new DatabaseDiff([SchemaDiff.Containing("app") with { Tables = [users] }])
            {
                // Both deployment bookends at the root; the change script rides its column above.
                DeploymentScripts =
                [
                    new DeploymentScript("seed", "INSERT INTO app.config VALUES (1)", null, DeploymentPhase.Pre) { RunCondition = RunCondition.Once },
                    new DeploymentScript("reindex", "REINDEX TABLE app.users", null, DeploymentPhase.Post) { RunOutsideTransaction = true },
                ],
            },
            [
                new SqlStatement("INSERT INTO app.config VALUES (1)"),
                new SqlStatement("CREATE INDEX CONCURRENTLY ...", RunOutsideTransaction: true),
                new SqlStatement("REINDEX TABLE app.users", RunOutsideTransaction: true),
            ],
            Managed: new IdentitySet(
                DatabaseObjects: [DatabaseAddress.Schema("app")],
                SchemaObjects: [ObjectAddress.Table("app", "users")]),
            Adopted: new IdentitySet(SchemaObjects: [ObjectAddress.Table("app", "users")]));

        return new PlanFileEnvelope(plan, DateTimeOffset.UnixEpoch);
    }

    [Fact]
    public void Serialize_ThenDeserialize_RoundTripsTheWholeEnvelope()
    {
        // Arrange
        var original = SampleEnvelope();

        var json = Json(original);

        // Act
        var roundTripped = _sut.Deserialize(_sut.Serialize(original));

        // Assert
        // A read + write cycle reproduces the exact same document, including the polymorphic script events and the diff.
        Json(roundTripped).ShouldBe(json);
    }

    [Fact]
    public void Serialize_StoresAChangeScriptTargetAsOneObject()
    {
        // Act
        var json = Json(SampleEnvelope());

        // Assert
        json.ShouldContain("\"target\": {");
        json.ShouldNotContain("\"tableName\":");
        json.ShouldNotContain("\"memberName\":");
    }

    [Fact]
    public void Serialize_StampsTheCurrentVersion_WithoutTheCallerSupplyingIt()
    {
        // Act
        // The caller never passes a version; the format owns it.
        var roundTripped = _sut.Deserialize(_sut.Serialize(SampleEnvelope()));

        // Assert
        roundTripped.Version.ShouldBe(PlanFileEnvelope.CurrentVersion);
        roundTripped.CreatedAt.ShouldBe(DateTimeOffset.UnixEpoch);
    }

    [Fact]
    public void Deserialize_RestoresConcreteScriptEventsInOrder()
    {
        // Arrange
        var roundTripped = _sut.Deserialize(_sut.Serialize(SampleEnvelope()));

        // The discriminator must reconstruct each concrete script record, not the abstract base, and keep order.
        roundTripped.Plan.Diff.AllScripts().Select(s => s.GetType()).ShouldBe(

            // Act
            [typeof(ChangeScript), typeof(DeploymentScript), typeof(DeploymentScript)]);

        // Assert
        roundTripped.Plan.Diff.AllScripts().ShouldBe(SampleEnvelope().Plan.Diff.AllScripts());
    }

    [Fact]
    public void Deserialize_RestoresStatementDetail()
    {
        // Act
        var roundTripped = _sut.Deserialize(_sut.Serialize(SampleEnvelope()));

        // Assert
        roundTripped.Plan.Statements.ShouldBe(SampleEnvelope().Plan.Statements);
        roundTripped.Plan.Statements[1].RunOutsideTransaction.ShouldBeTrue();
    }

    [Fact]
    public void Deserialize_RestoresWhatTheApplyManagesAndAdopts()
    {
        // Act
        var roundTripped = _sut.Deserialize(_sut.Serialize(SampleEnvelope()));

        // Assert — a saved plan is applied later, so what it takes over has to survive the file.
        roundTripped.Plan.Managed.DatabaseObjects.ShouldBe([DatabaseAddress.Schema("app")]);
        roundTripped.Plan.Managed.SchemaObjects.ShouldBe([ObjectAddress.Table("app", "users")]);
        roundTripped.Plan.Adopted.DatabaseObjects.ShouldBeEmpty();
        roundTripped.Plan.Adopted.SchemaObjects.ShouldBe([ObjectAddress.Table("app", "users")]);
    }

    [Fact]
    public async Task Write_ThenRead_RoundTripsThroughAFile()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), $"nschema-plan-{Guid.NewGuid():N}.json");
        try
        {
            await _sut.Write(path, SampleEnvelope(), TestContext.Current.CancellationToken);

            // Act
            var roundTripped = await _sut.Read(path, TestContext.Current.CancellationToken);

            // Assert
            Json(roundTripped.Require()).ShouldBe(Json(SampleEnvelope()));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Serialize_ThenDeserialize_RoundTripsDiffAnnotations()
    {
        // Arrange — a diff whose column add is annotated with its matched script, so diff-node persistence and
        // the script-event discriminator inside the diff are both exercised.
        var migration = new ChangeScript("backfill_emails", "UPDATE app.users SET email = ''",
            new ChangeTarget("app", "users", "email", ChangeTrigger.AddColumn))
        {
            RunOutsideTransaction = true,
        };
        var diff = new DatabaseDiff(
        [
            SchemaDiff.Containing("app") with
            {
                Tables = [
                TableDiff.Modified("app", "users") with
                {
                    Columns = [
                    ColumnDiff.Added(new Column { Name = "email", Type = SqlType.Text }) with{ MigrationScript = migration },
                ],
                },
            ],
            },
        ]);
        var envelope = new PlanFileEnvelope(
            new MigrationPlan(diff, [new SqlStatement("UPDATE app.users SET email = ''", RunOutsideTransaction: true)]),
            DateTimeOffset.UnixEpoch);

        // Act
        var roundTripped = _sut.Deserialize(_sut.Serialize(envelope));

        // Assert — record equality covers every field, including the init-only RunOutsideTransaction.
        roundTripped.Plan.Diff.Schemas[0].Tables[0].Columns[0].MigrationScript.ShouldBe(migration);
        roundTripped.Plan.Diff.AllScripts().ShouldHaveSingleItem().ShouldBe(migration);
    }

    [Fact]
    public void Deserialize_Garbage_ThrowsPlanFileDeserializationException()
    {
        // Act
        var garbage = Encoding.UTF8.GetBytes("not json at all");

        // Assert
        Should.Throw<PlanFileDeserializationException>(() => _sut.Deserialize(garbage));
    }
}
