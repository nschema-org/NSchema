using NSchema.Configuration.Domain;
using NSchema.Configuration.Plugins;
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
using NSchema.State.Locks;
using Spectre.Console.Testing;

namespace NSchema.Tests.Services;

/// <summary>
/// Snapshot coverage for <see cref="SpectreConsoleReporter"/>: the narration script replayed at each verbosity
/// (pinning exactly which lines each level shows), and every artifact at both its full and its quiet face. The
/// document artifacts mirror the Markdown reporter's cases, so the two text faces are pinned against the same inputs.
/// </summary>
public sealed class SpectreConsoleReporterSnapshotTests
{
    private readonly TestConsole _out = new();
    private readonly TestConsole _error = new();
    private readonly SpectreConsoleReporter _sut;

    public SpectreConsoleReporterSnapshotTests()
    {
        _out.Profile.Width = 200;
        _error.Profile.Width = 200;
        _sut = Build(Verbosity.Normal);
    }

    private SpectreConsoleReporter Build(Verbosity verbosity) => new(_out, _error, verbosity);

    private Task VerifyStreams() => Verify($"── stdout ──\n{_out.Output}\n── stderr ──\n{_error.Output}");

    // ── Narration ─────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// One of every narration the CLI emits, so the per-verbosity snapshots record the complete gating rule.
    /// </summary>
    private static void PlayNarrationScript(SpectreConsoleReporter reporter)
    {
        reporter.ReportEnvironment("staging");
        reporter.Announce($"Applying schema migration. Changes will be applied to the {"orders"} database.");
        reporter.Report(MessageKind.Progress, "Generating SQL...");
        reporter.Report(MessageKind.Verbose, "Resolved provider plugin NSchema.Postgres 5.0.0.");
        reporter.Detail($"The lock is held until you run: nschema lock release");
        reporter.Warn($"Drift detected: {"1 changed"}.");
        reporter.Success($"Apply complete. {"2 added"}.");
    }

    [Fact]
    public Task Narration_QuietVerbosity()
    {
        PlayNarrationScript(Build(Verbosity.Quiet));

        return VerifyStreams();
    }

    [Fact]
    public Task Narration_NormalVerbosity()
    {
        PlayNarrationScript(_sut);

        return VerifyStreams();
    }

    [Fact]
    public Task Narration_VerboseVerbosity()
    {
        PlayNarrationScript(Build(Verbosity.Verbose));

        return VerifyStreams();
    }

    // ── Diagnostics ───────────────────────────────────────────────────────────────────────────────────────────────

    private static Diagnostic[] MixedDiagnostics() =>
    [
        new Diagnostic("run-once", "run-once", "Skipping 'seed-users': already executed.", DiagnosticSeverity.Info),
        new Diagnostic("drift", "drift-detected", "Recorded state differs from the live database.", DiagnosticSeverity.Warning),
        new Diagnostic("destructive-actions", "destructive-change", "Dropping column id", DiagnosticSeverity.Error),
    ];

    [Fact]
    public Task ReportDiagnostics_MixedSeverities()
    {
        _sut.ReportDiagnostics(MixedDiagnostics());

        return VerifyStreams();
    }

    [Fact]
    public Task ReportDiagnostics_Empty()
    {
        _sut.ReportDiagnostics([]);

        return VerifyStreams();
    }

    [Fact]
    public Task ReportDiagnostics_QuietVerbosity_KeepsOnlyTheActionableRows()
    {
        Build(Verbosity.Quiet).ReportDiagnostics(MixedDiagnostics());

        return VerifyStreams();
    }

    // ── Query artifacts ───────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public Task ReportLockInfo_HeldWithExpiry()
    {
        // An epoch expiry is always in the past, so this also pins the "(expired)" annotation.
        var info = new StateLockInfo(new LockId("abc123"), "apply", new LockHolder("tom@dev"), DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(30));

        _sut.ReportLockInfo(info);

        return VerifyStreams();
    }

    private static ScriptHashEntry[] DeclaredScripts() =>
    [
        new ScriptHashEntry("seed-users", "abc123"),
        new ScriptHashEntry("backfill", "def456"),
    ];

    [Fact]
    public Task ReportScriptHashes_DeclaredScripts()
    {
        _sut.ReportScriptHashes(DeclaredScripts());

        return VerifyStreams();
    }

    [Fact]
    public Task ReportScriptHashes_QuietVerbosity()
    {
        Build(Verbosity.Quiet).ReportScriptHashes(DeclaredScripts());

        return VerifyStreams();
    }

    private static ProjectPlugin[] ProjectPlugins() =>
    [
        new ProjectPlugin("provider", "postgres", "NSchema.Postgres", SemanticVersion.Parse("4.0.0"), Restored: true, CachePath: "/c"),
        new ProjectPlugin("backend", "s3", "NSchema.Aws", SemanticVersion.Parse("4.1.0"), Restored: false, CachePath: null),
    ];

    [Fact]
    public Task ReportProjectPlugins_RestoredAndMissing()
    {
        _sut.ReportProjectPlugins(ProjectPlugins());

        return VerifyStreams();
    }

    [Fact]
    public Task ReportProjectPlugins_QuietVerbosity()
    {
        Build(Verbosity.Quiet).ReportProjectPlugins(ProjectPlugins());

        return VerifyStreams();
    }

    [Fact]
    public Task ReportPluginDetail_NotRestored()
    {
        _sut.ReportPluginDetail(
            new ProjectPlugin("backend", "s3", "NSchema.Aws", SemanticVersion.Parse("4.0.0"), Restored: false, CachePath: null));

        return VerifyStreams();
    }

    private static CachedPlugin[] CachedPlugins() =>
    [
        new CachedPlugin("NSchema.Postgres", SemanticVersion.Parse("4.0.0"), "/c", 2 * 1024 * 1024),
        new CachedPlugin("NSchema.Sqlite", SemanticVersion.Parse("4.2.0"), "/s", 512),
    ];

    [Fact]
    public Task ReportCachedPlugins_SizedListing()
    {
        _sut.ReportCachedPlugins("/cache/root", CachedPlugins());

        return VerifyStreams();
    }

    [Fact]
    public Task ReportCachedPlugins_QuietVerbosity()
    {
        Build(Verbosity.Quiet).ReportCachedPlugins("/cache/root", CachedPlugins());

        return VerifyStreams();
    }

    private static OutdatedPlugin[] OutdatedPlugins() =>
    [
        new OutdatedPlugin("provider", "postgres", "NSchema.Postgres", SemanticVersion.Parse("5.0.0"), SemanticVersion.Parse("5.2.0"), SemanticVersion.Parse("5.2.0"), Outdated: true),
        new OutdatedPlugin("backend", "s3", "NSchema.Aws", SemanticVersion.Parse("5.2.0"), SemanticVersion.Parse("5.2.0"), SemanticVersion.Parse("5.2.0"), Outdated: false),
    ];

    [Fact]
    public Task ReportOutdatedPlugins_MixedCurrency()
    {
        _sut.ReportOutdatedPlugins(OutdatedPlugins());

        return VerifyStreams();
    }

    [Fact]
    public Task ReportOutdatedPlugins_QuietVerbosity()
    {
        Build(Verbosity.Quiet).ReportOutdatedPlugins(OutdatedPlugins());

        return VerifyStreams();
    }

    // ── Document artifacts ────────────────────────────────────────────────────────────────────────────────────────

    // A diff exercising every marker: an added schema and table (+), a modified table with a type change (~) and a
    // dropped column (-), and a removed schema (-). 'app' is Touched — in the diff only because its tables changed —
    // so it contributes its contents and no header line of its own.
    private static DatabaseDiff RichDiff() => new(
    [
        SchemaDiff.Added("reporting"),
        SchemaDiff.Containing("app") with
        {
            Tables =
            [
                TableDiff.Added("app", new Table { Name = "users" }) with
                {
                    Columns = [ColumnDiff.Added(new Column { Name = "id", Type = SqlType.BigInt })],
                },
                TableDiff.Modified("app", "orders") with
                {
                    Columns =
                    [
                        ColumnDiff.Modified(new Column { Name = "total", Type = SqlType.BigInt })
                            with { Type = new ValueChange<SqlType>(SqlType.Int, SqlType.BigInt) },
                        ColumnDiff.Removed(new Column { Name = "legacy", Type = SqlType.Boolean }),
                    ],
                },
            ],
        },
        SchemaDiff.Removed("scratch"),
    ]);

    [Fact]
    public Task ReportDiff_RichDiff()
    {
        _sut.ReportDiff(RichDiff());

        return Verify(_out.Output);
    }

    [Fact]
    public Task ReportDiff_EmptyDiff()
    {
        _sut.ReportDiff(new DatabaseDiff());

        return Verify(_out.Output);
    }

    [Fact]
    public Task ReportDiff_TouchedSchema()
    {
        // A schema carried by its contents alone renders no header — only what actually changed inside it. Pinned on
        // its own (RichDiff covers it among four other markers) so a regression names the case it broke.
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

        _sut.ReportDiff(diff);

        return Verify(_out.Output);
    }

    [Fact]
    public Task ReportDiff_WithDeploymentScripts()
    {
        // The scripts ride the diff, annotated with their deployment event; a run-once script reads the same way.
        var diff = new DatabaseDiff([SchemaDiff.Added("app")])
        {
            DeploymentScripts =
            [
                new DeploymentScript("seed-roles", "INSERT INTO app.roles VALUES ('admin');", ScopeSchema: null, DeploymentPhase.Pre) { RunCondition = RunCondition.Once },
                new DeploymentScript("refresh-views", "REFRESH MATERIALIZED VIEW app.stats;", ScopeSchema: null, DeploymentPhase.Post),
            ],
        };

        _sut.ReportDiff(diff);

        return Verify(_out.Output);
    }

    [Fact]
    public Task ReportDiff_QuietVerbosity()
    {
        Build(Verbosity.Quiet).ReportDiff(RichDiff());

        return Verify(_out.Output);
    }

    [Fact]
    public Task ReportPlan_AdoptionOnly()
    {
        // The database already matches the project: no diff, no SQL, and the objects change hands.
        var plan = new MigrationPlan(new DatabaseDiff(), [])
        {
            Adopted = new IdentitySet(
                DatabaseObjects: [DatabaseAddress.Schema("app")],
                SchemaObjects: [ObjectAddress.Table("app", "users")]),
        };

        _sut.ReportPlan(plan);

        return Verify(_out.Output);
    }

    [Fact]
    public Task ReportPlan_ChangesAndAdoptions()
    {
        // Adoption lists after the diff lines: nothing is done to those objects, so no marker fits them.
        var plan = new MigrationPlan(RichDiff(), [])
        {
            Adopted = new IdentitySet(SchemaObjects: [ObjectAddress.Table("app", "legacy_audit")]),
        };

        _sut.ReportPlan(plan);

        return Verify(_out.Output);
    }

    private static MigrationPlan PlanWithSql() => new(new DatabaseDiff([SchemaDiff.Added("app")]),
    [
        new SqlStatement("CREATE TABLE app.users (\n    id bigint NOT NULL\n)", RunOutsideTransaction: false),
        new SqlStatement("CREATE INDEX CONCURRENTLY users_id_ix ON app.users (id)", RunOutsideTransaction: true),
    ]);

    [Fact]
    public Task ReportPlan_WithSql()
    {
        _sut.ReportPlan(PlanWithSql());

        return Verify(_out.Output);
    }

    [Fact]
    public Task ReportPlan_NoStatements()
    {
        // An apply that executes nothing gets no SQL section; the plan section has already said as much.
        _sut.ReportPlan(new MigrationPlan(new DatabaseDiff(), []));

        return Verify(_out.Output);
    }

    [Fact]
    public Task ReportPlan_QuietVerbosity()
    {
        // The whole point of quiet mode: an automated run's plan collapses to one line instead of a full dump.
        Build(Verbosity.Quiet).ReportPlan(PlanWithSql());

        return Verify(_out.Output);
    }

    private static Database SmallSchema() => new()
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

    [Fact]
    public Task ReportDatabase()
    {
        _sut.ReportDatabase(SmallSchema());

        return Verify(_out.Output);
    }

    [Fact]
    public Task ReportDatabase_QuietVerbosity()
    {
        Build(Verbosity.Quiet).ReportDatabase(SmallSchema());

        return Verify(_out.Output);
    }

    /// <summary>A recorded schema holding one managed table and one NSchema knows nothing about.</summary>
    private static Database PartlyManaged() => new()
    {
        Schemas =
        [
            new Schema
            {
                Name = "app",
                Tables =
                [
                    new Table { Name = "widgets", Columns = [new Column { Name = "id", Type = SqlType.BigInt }] },
                    new Table { Name = "legacy_audit", Columns = [new Column { Name = "id", Type = SqlType.BigInt }] },
                ],
            },
        ],
    };

    private static IdentitySet ManagedInApp() => new(
        DatabaseObjects: [DatabaseAddress.Schema("app")],
        SchemaObjects: [ObjectAddress.Table("app", "widgets")]);

    private static ScriptExecution[] RecordedScripts() =>
    [
        new ScriptExecution(new ScriptReference("app", "backfill"), new ScriptHash("def456"), DateTimeOffset.UnixEpoch.AddDays(1)),
        new ScriptExecution(new ScriptReference(null, "seed-users"), new ScriptHash("abc123"), DateTimeOffset.UnixEpoch),
    ];

    [Fact]
    public Task ReportState()
    {
        // The recorded schema carries more than NSchema manages, so the rest is marked and counted; an empty
        // ledger still gets its section, so the report has a fixed shape.
        _sut.ReportState(new DatabaseState(PartlyManaged(), []) { Managed = ManagedInApp() });

        return Verify(_out.Output);
    }

    [Fact]
    public Task ReportState_WithRecordedScripts()
    {
        _sut.ReportState(new DatabaseState(PartlyManaged(), RecordedScripts()) { Managed = ManagedInApp() });

        return Verify(_out.Output);
    }

    [Fact]
    public Task ReportState_QuietVerbosity()
    {
        Build(Verbosity.Quiet).ReportState(new DatabaseState(PartlyManaged(), RecordedScripts()) { Managed = ManagedInApp() });

        return Verify(_out.Output);
    }

    [Fact]
    public Task ReportScripts()
    {
        // `script list` reports the ledger on its own — the same section the recorded state carries.
        _sut.ReportScripts(
        [
            new ScriptExecution(new ScriptReference(null, "seed-users"), new ScriptHash("abc123"), DateTimeOffset.UnixEpoch),
            new ScriptExecution(new ScriptReference("app", "backfill"), new ScriptHash("def456"), DateTimeOffset.UnixEpoch.AddDays(1)),
        ]);

        return Verify(_out.Output);
    }

    [Fact]
    public Task ReportScripts_NothingRecorded()
    {
        _sut.ReportScripts([]);

        return Verify(_out.Output);
    }

    [Fact]
    public Task ReportScripts_QuietVerbosity()
    {
        Build(Verbosity.Quiet).ReportScripts(RecordedScripts());

        return Verify(_out.Output);
    }
}
