using System.Text.Json;
using NSchema.Diff.Domain;
using NSchema.Diff.Domain.Schemas;
using NSchema.Model;
using NSchema.Model.Schemas;
using NSchema.Model.Scripts;
using NSchema.Plan.Domain;
using NSchema.Services.Reporting;
using NSchema.State.Domain;

namespace NSchema.Tests.Services;

public sealed class JsonConsolePresenterTests
{
    private readonly StringWriter _out = new();
    private readonly JsonConsolePresenter _sut;

    public JsonConsolePresenterTests() => _sut = new JsonConsolePresenter(_out);

    private List<JsonElement> StdoutLines() => _out.ToString()
        .Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Select(line => JsonDocument.Parse(line).RootElement)
        .ToList();

    /// <summary>The one object a single report writes.</summary>
    private JsonElement Reported() => StdoutLines().ShouldHaveSingleItem();

    [Fact]
    public void ReportDiff_EmptyDiff_EmitsTheDiffItself()
    {
        _sut.ReportDiff(new DatabaseDiff());

        Reported().GetProperty("isEmpty").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public void ReportDiff_NonEmptyDiff_EmitsTheDiffItself()
    {
        _sut.ReportDiff(new DatabaseDiff([SchemaDiff.Added("app")]));

        Reported().GetProperty("isEmpty").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public void ReportDiff_TouchedSchema_CarriesTheChangeKindVerbatim()
    {
        // The text faces drop a touched schema's header, but --json is the structured face: it reports the schema as
        // the diff models it, so a consumer can tell "carried by its contents" from an actual schema change. This is
        // the only surface the kind is visible on, hence the only one that can pin its wire spelling.
        _sut.ReportDiff(new DatabaseDiff([SchemaDiff.Containing("app")]));

        var schema = Reported().GetProperty("schemas")[0];
        schema.GetProperty("name").GetString().ShouldBe("app");
        schema.GetProperty("change").GetString().ShouldBe("touched");
    }

    [Fact]
    public void ReportDiff_CarriesTheDeploymentScriptsOnTheDiff()
    {
        // The scripts are first-class on the diff, so the diff carries them — there is no separate scripts object.
        var diff = new DatabaseDiff([SchemaDiff.Added("app")])
        {
            DeploymentScripts =
            [
                new DeploymentScript("seed-roles", "INSERT INTO app.roles VALUES ('admin');", ScopeSchema: null, DeploymentPhase.Pre)
                {
                    RunCondition = RunCondition.Once,
                },
            ],
        };

        _sut.ReportDiff(diff);

        var script = Reported().GetProperty("deploymentScripts")[0];
        script.GetProperty("name").GetString().ShouldBe("seed-roles");
        script.GetProperty("phase").GetString().ShouldBe("pre");
        script.GetProperty("runCondition").GetString().ShouldBe("once");
    }

    [Fact]
    public void ReportPlan_FoldsTheDiffAdoptionsAndSqlIntoOneObject()
    {
        // A plan read back from a saved file is the same artifact as a freshly computed one, so `plan show` answers
        // exactly as `plan` does.
        var diff = new DatabaseDiff([SchemaDiff.Added("app")])
        {
            DeploymentScripts = [new DeploymentScript("seed-roles", "INSERT INTO app.roles VALUES ('admin');", ScopeSchema: null, DeploymentPhase.Pre)],
        };
        var plan = new MigrationPlan(diff, [new SqlStatement("CREATE TABLE app.widgets ()", RunOutsideTransaction: false)])
        {
            Adopted = new IdentitySet(SchemaObjects: [ObjectAddress.Table("app", "users")]),
        };

        _sut.ReportPlan(plan);

        var reported = Reported();
        reported.GetProperty("diff").GetProperty("deploymentScripts")[0].GetProperty("name").GetString().ShouldBe("seed-roles");
        reported.GetProperty("adopted").GetProperty("schemaObjects")[0].GetProperty("name").GetString().ShouldBe("users");
        reported.GetProperty("sql")[0].GetProperty("sql").GetString()!.ShouldContain("CREATE TABLE app.widgets");
    }

    [Fact]
    public void ReportPlan_EmitsStatementsWithTransactionFlag()
    {
        _sut.ReportPlan(new MigrationPlan(new DatabaseDiff(),
            [new SqlStatement("CREATE INDEX CONCURRENTLY i ON t (c)", RunOutsideTransaction: true)]));

        var statement = Reported().GetProperty("sql")[0];
        statement.GetProperty("sql").GetString()!.ShouldContain("CONCURRENTLY");
        statement.GetProperty("runOutsideTransaction").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public void ReportPlan_NothingAdopted_StillCarriesTheKey()
    {
        // The shape is fixed, so a consumer reads `adopted` without probing for it first.
        _sut.ReportPlan(new MigrationPlan(new DatabaseDiff([SchemaDiff.Added("app")]), []));

        Reported().GetProperty("adopted").GetProperty("schemaObjects").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public void ReportDatabase_EmitsTheSchemaAsTheWholeObject()
    {
        _sut.ReportDatabase(new Database { Schemas = [new Schema { Name = "app" }] });

        var reported = Reported();
        reported.ValueKind.ShouldBe(JsonValueKind.Object);
        reported.GetProperty("schemas")[0].GetProperty("name").GetString().ShouldBe("app");
    }

    [Fact]
    public void ReportState_EmitsTheSchemaWhatIsManagedOfIt_AndTheLedger()
    {
        // Keyed as the state payload keys them, so `state show` and a pulled payload read the same way.
        var state = new DatabaseState(
            new Database { Schemas = [new Schema { Name = "app" }] },
            [new ScriptExecution(new ScriptReference(null, "seed-users"), new ScriptHash("abc123"), DateTimeOffset.UnixEpoch)])
        {
            Managed = new IdentitySet(SchemaObjects: [ObjectAddress.Table("app", "users")]),
        };

        _sut.ReportState(state);

        var reported = Reported();
        reported.GetProperty("database").GetProperty("schemas")[0].GetProperty("name").GetString().ShouldBe("app");
        reported.GetProperty("managed").GetProperty("schemaObjects")[0].GetProperty("name").GetString().ShouldBe("users");
        // The ledger reads the same here as it does from `script list` — one shape, wherever it is reported.
        reported.GetProperty("scripts")[0].GetProperty("name").GetString().ShouldBe("seed-users");
    }

    [Fact]
    public void ReportScripts_EmitsASingleArray()
    {
        // A query result: one clean array on stdout a script can consume directly.
        _sut.ReportScripts([new ScriptExecution(new ScriptReference(null, "seed-users"), new ScriptHash("abc123"), DateTimeOffset.UnixEpoch)]);

        var record = Reported().EnumerateArray().ShouldHaveSingleItem();
        record.GetProperty("name").GetString().ShouldBe("seed-users");
        record.GetProperty("hash").GetString().ShouldBe("abc123");
        record.GetProperty("executedUtc").GetDateTimeOffset().ShouldBe(DateTimeOffset.UnixEpoch);
    }

    [Fact]
    public void ReportScripts_NothingRecorded_EmitsAnEmptyArray()
    {
        // An empty ledger is an empty array rather than nothing at all, so `| jq length` answers on every run.
        _sut.ReportScripts([]);

        var reported = Reported();
        reported.ValueKind.ShouldBe(JsonValueKind.Array);
        reported.GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public void ReportScripts_ScopedScript_NamesItByItsReference()
    {
        // A schema-scoped script is `schema.name`: the same spelling the ledger and `script taint` use.
        _sut.ReportScripts([new ScriptExecution(new ScriptReference("app", "backfill"), new ScriptHash("abc123"), DateTimeOffset.UnixEpoch)]);

        Reported().EnumerateArray().ShouldHaveSingleItem().GetProperty("name").GetString().ShouldBe("app.backfill");
    }

    [Fact]
    public void Output_IsNewlineDelimited_OneObjectPerLine()
    {
        // Each report is one complete JSON document on its own line, so a run emitting several stays parseable.
        _sut.ReportDiff(new DatabaseDiff());
        _sut.ReportPlan(new MigrationPlan(new DatabaseDiff(), []));

        StdoutLines().Count.ShouldBe(2);
    }
}
