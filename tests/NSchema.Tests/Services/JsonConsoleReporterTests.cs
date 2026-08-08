using System.Text.Json;
using NSchema.Configuration.Domain;
using NSchema.Configuration.Plugins;
using NSchema.Diff.Domain;
using NSchema.Diff.Domain.Schemas;
using NSchema.Model;
using NSchema.Model.Schemas;
using NSchema.Model.Scripts;
using NSchema.Plan.Domain;
using NSchema.Services.Reporting;
using NSchema.State.Domain;
using NSchema.State.Locks;
using Spectre.Console.Testing;

namespace NSchema.Tests.Services;

public sealed class JsonConsoleReporterTests
{
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();
    private readonly TestConsole _interaction = new();
    private readonly JsonConsoleReporter _sut;

    public JsonConsoleReporterTests()
    {
        _interaction.Profile.Width = 200;
        _sut = Build(Verbosity.Normal);
    }

    private JsonConsoleReporter Build(Verbosity verbosity) => new(verbosity, _out, _error, _interaction);

    private List<JsonElement> StderrEvents() => _error.ToString()
        .Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Select(line => JsonDocument.Parse(line).RootElement)
        .ToList();

    private List<JsonElement> StdoutEvents() => _out.ToString()
        .Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Select(line => JsonDocument.Parse(line).RootElement)
        .ToList();

    /// <summary>The one object a single report writes.</summary>
    private JsonElement Reported() => StdoutEvents().ShouldHaveSingleItem();

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
    public void Detail_EmitsALogEventWithItsOwnLevel()
    {
        // Act — a detail line is its own kind, so a log consumer can tell a hint from an announcement.
        _sut.Detail($"The lock is held until you run: nschema lock release");

        // Assert
        var evt = StderrEvents().ShouldHaveSingleItem();
        evt.GetProperty("level").GetString().ShouldBe("detail");
    }

    [Fact]
    public void Detail_QuietVerbosity_IsSuppressed()
    {
        Build(Verbosity.Quiet).Detail($"The lock is held until you run: nschema lock release");

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

    private static ConfirmationRequest Confirmation(bool autoApprove) =>
        new($"NSchema will execute {3} statement(s) against the database.")
        {
            Question = "Do you want to apply these changes?",
            SkipFlag = "--auto-approve",
            AutoApprove = autoApprove,
        };

    [Fact]
    public void Confirm_AutoApprove_EmitsGatedLogEvents_LeavingStdoutClean()
    {
        // Act
        Should.NotThrow(() => _sut.Confirm(Confirmation(autoApprove: true)));

        // Assert — the pre-approved confirmation is narration: log events on stderr, nothing on stdout.
        _out.ToString().ShouldBeEmpty();
        StderrEvents().ShouldContain(evt => evt.GetProperty("message").GetString()!.Contains("Auto-approve"));
    }

    [Fact]
    public void Confirm_AutoApproveUnderQuiet_IsSilent()
    {
        // Act
        Should.NotThrow(() => Build(Verbosity.Quiet).Confirm(Confirmation(autoApprove: true)));

        // Assert
        _out.ToString().ShouldBeEmpty();
        _error.ToString().ShouldBeEmpty();
    }

    [Fact]
    public void Confirm_Interactive_PromptsOffStdout()
    {
        // Arrange
        _interaction.Interactive();
        _interaction.Input.PushTextWithEnter("yes");

        // Act
        Should.NotThrow(() => _sut.Confirm(Confirmation(autoApprove: false)));

        // Assert — the summary and prompt render on the interaction console (stderr), never the result stream.
        _out.ToString().ShouldBeEmpty();
        _interaction.Output.ShouldContain("execute 3 statement(s)");
    }

    [Fact]
    public void Confirm_Throws_WhenUserTypesAnythingElse()
    {
        // Arrange
        _interaction.Interactive();
        _interaction.Input.PushTextWithEnter("no");

        // Act / Assert
        Should.Throw<ConfirmationDeclinedException>(() => _sut.Confirm(Confirmation(autoApprove: false)));
    }

    [Fact]
    public void Confirm_Throws_WhenNotInteractive()
    {
        // Act / Assert — redirected stdin / CI has no input to read; failing loudly beats a silent no-op exit 0.
        var ex = Should.Throw<ConfirmationDeclinedException>(() => _sut.Confirm(Confirmation(autoApprove: false)));
        ex.Message.ShouldContain("no interactive terminal");
        ex.Message.ShouldContain("--auto-approve");
    }

    [Fact]
    public void Confirm_NotInteractive_KeepsBothStreamsMachineReadable()
    {
        // Act — the CI shape: --json with no terminal and no --auto-approve.
        Should.Throw<ConfirmationDeclinedException>(() => _sut.Confirm(Confirmation(autoApprove: false)));

        // Assert — stdout untouched, and the summary rides the log stream rather than being written raw, so a
        // redirected stderr stays uniform NDJSON.
        _out.ToString().ShouldBeEmpty();
        StderrEvents().ShouldHaveSingleItem().GetProperty("message").GetString()!.ShouldContain("execute 3 statement(s)");
    }

    [Fact]
    public void Confirm_NotInteractiveUnderQuiet_WritesNothing()
    {
        // Act — quiet gates the summary log event; the thrown message is what surfaces the refusal.
        Should.Throw<ConfirmationDeclinedException>(() => Build(Verbosity.Quiet).Confirm(Confirmation(autoApprove: false)));

        // Assert
        _out.ToString().ShouldBeEmpty();
        _error.ToString().ShouldBeEmpty();
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
        var info = new StateLockInfo(new LockId("abc"), "apply", new LockHolder("tom@dev"), DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(30));

        _sut.ReportLockInfo(info);

        var evt = StdoutEvents().ShouldHaveSingleItem();
        evt.GetProperty("locked").GetBoolean().ShouldBeTrue();
        evt.GetProperty("lockId").GetString().ShouldBe("abc");
        evt.GetProperty("operation").GetString().ShouldBe("apply");
        evt.GetProperty("who").GetString().ShouldBe("tom@dev");
        evt.GetProperty("expires").ValueKind.ShouldNotBe(JsonValueKind.Null);
    }

    [Fact]
    public void ReportScriptHashes_EmitsASingleArray()
    {
        _sut.ReportScriptHashes([new ScriptHashEntry("seed-users", "abc123")]);

        var evt = StdoutEvents().ShouldHaveSingleItem();
        var record = evt.EnumerateArray().ShouldHaveSingleItem();
        record.GetProperty("name").GetString().ShouldBe("seed-users");
        record.GetProperty("hash").GetString().ShouldBe("abc123");
    }

    [Fact]
    public void ReportProjectPlugins_EmitsArrayWithRoleAndCacheStatus()
    {
        // Arrange
        var plugins = new[]
        {
            new ProjectPlugin("provider", "postgres", "NSchema.Postgres", SemanticVersion.Parse("4.0.0"), Restored: true, CachePath: "/c"),
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
        var cached = new[] { new CachedPlugin("NSchema.Postgres", SemanticVersion.Parse("4.0.0"), "/c", 2048) };

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
        _sut.ReportPluginDetail(new ProjectPlugin("backend", "s3", "NSchema.Aws", SemanticVersion.Parse("4.0.0"), Restored: false, CachePath: null));

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
        _sut.ReportDiagnostics((Diagnostic[])[new Diagnostic("destructive-actions", "destructive-change", "Dropping column id", DiagnosticSeverity.Error)]);

        // Assert
        var evt = StdoutEvents().ShouldHaveSingleItem();
        evt.GetProperty("type").GetString().ShouldBe("diagnostics");
        evt.GetProperty("diagnostics")[0].GetProperty("message").GetString().ShouldBe("Dropping column id");
    }

    [Fact]
    public void ReportDiagnostics_Empty_EmitsNothing()
    {
        _sut.ReportDiagnostics([]);

        _out.ToString().ShouldBeEmpty();
    }

    [Fact]
    public void ReportDiagnostics_QuietVerbosity_KeepsInfoRows()
    {
        // Act — the diagnostics event is a result; --quiet gates only the log stream, never the machine contract.
        Build(Verbosity.Quiet).ReportDiagnostics((Diagnostic[])[new Diagnostic("run-once", "run-once", "Skipping 'seed-users'.", DiagnosticSeverity.Info)]);

        // Assert
        Reported().GetProperty("diagnostics")[0].GetProperty("severity").GetString().ShouldBe("info");
    }

    [Fact]
    public void ReportOutdatedPlugins_EmitsArrayWithCurrentWantedLatest()
    {
        // Arrange
        var plugins = new[]
        {
            new OutdatedPlugin("provider", "postgres", "NSchema.Postgres", SemanticVersion.Parse("5.0.0"), SemanticVersion.Parse("5.2.0"), SemanticVersion.Parse("5.2.0"), Outdated: true),
        };

        // Act
        _sut.ReportOutdatedPlugins(plugins);

        // Assert — a single clean array (a structured query result, not the NDJSON log stream).
        var array = StdoutEvents().ShouldHaveSingleItem();
        array.ValueKind.ShouldBe(JsonValueKind.Array);
        array[0].GetProperty("current").GetString().ShouldBe("5.0.0");
        array[0].GetProperty("wanted").GetString().ShouldBe("5.2.0");
        array[0].GetProperty("latest").GetString().ShouldBe("5.2.0");
        array[0].GetProperty("outdated").GetBoolean().ShouldBeTrue();
    }

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
    public void ReportPlan_QuietVerbosity_StillEmitsTheCompleteResult()
    {
        // Act — the artifact is the machine-readable result; --quiet gates only the log stream. `cmd --json --quiet`
        // still pipes a complete plan into jq.
        Build(Verbosity.Quiet).ReportPlan(new MigrationPlan(new DatabaseDiff([SchemaDiff.Added("app")]),
            [new SqlStatement("CREATE SCHEMA app", RunOutsideTransaction: false)]));

        // Assert
        var reported = Reported();
        reported.GetProperty("diff").GetProperty("isEmpty").GetBoolean().ShouldBeFalse();
        reported.GetProperty("sql").GetArrayLength().ShouldBe(1);
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
    public void ReportDatabase_QuietVerbosity_StillEmitsTheCompleteSchema()
    {
        Build(Verbosity.Quiet).ReportDatabase(new Database { Schemas = [new Schema { Name = "app" }] });

        Reported().GetProperty("schemas")[0].GetProperty("name").GetString().ShouldBe("app");
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

        StdoutEvents().Count.ShouldBe(2);
    }
}
