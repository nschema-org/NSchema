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

public sealed class SpectreConsoleReporterTests
{
    private readonly TestConsole _out = new();
    private readonly TestConsole _error = new();
    private readonly SpectreConsoleReporter _sut;

    public SpectreConsoleReporterTests()
    {
        _out.Profile.Width = 200;
        _error.Profile.Width = 200;
        _sut = Build(Verbosity.Normal);
    }

    private SpectreConsoleReporter Build(Verbosity verbosity) => new(_out, _error, verbosity);

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
    [InlineData(MessageKind.Detail)]
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
    public void Detail_WritesAnIndentedSecondaryLine()
    {
        // Act
        _sut.Detail($"The lock is held until you run: nschema lock release");

        // Assert
        _out.Output.ShouldContain("  The lock is held until you run:");
    }

    [Fact]
    public void Detail_QuietVerbosity_IsSuppressed()
    {
        // Act — a hint line is narration; quiet has asked for outcomes only.
        Build(Verbosity.Quiet).Detail($"The lock is held until you run: nschema lock release");

        // Assert
        _out.Output.ShouldBeEmpty();
    }

    [Fact]
    public void ReportEnvironment_PrintsTheBanner()
    {
        // Act
        _sut.ReportEnvironment("staging");

        // Assert
        _out.Output.ShouldContain("Environment:");
        _out.Output.ShouldContain("staging");
    }

    [Fact]
    public void ReportEnvironment_QuietVerbosity_IsSuppressed()
    {
        // Act — the banner is narration, so it gates with the rest of it.
        Build(Verbosity.Quiet).ReportEnvironment("staging");

        // Assert
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
    public void HighlightedHole_WithMarkupCharacters_IsEscaped()
    {
        // Arrange — array types contain square brackets, which are Spectre markup delimiters.
        var name = "app.events [text[]]";

        // Act
        _sut.Success($"Imported {name}");

        // Assert — escaped, not interpreted or leaked.
        _out.Output.ShouldContain("app.events [text[]]");
    }

    private static ConfirmationRequest Confirmation(bool autoApprove, bool destructive = false) =>
        new($"NSchema will execute {3} statement(s) against the database.")
        {
            Question = "Do you want to apply these changes?",
            SkipFlag = "--auto-approve",
            AutoApprove = autoApprove,
            Destructive = destructive,
        };

    [Fact]
    public void Confirm_Proceeds_WhenAutoApprove()
    {
        // Act
        Should.NotThrow(() => _sut.Confirm(Confirmation(autoApprove: true)));

        // Assert — the summary is shown and the prompt is skipped.
        _out.Output.ShouldContain("Auto-approve");
    }

    [Fact]
    public void Confirm_AutoApproveUnderQuiet_IsSilent()
    {
        // Act — pre-approved, nothing is asked, so the confirmation is narration and gates with it.
        Should.NotThrow(() => Build(Verbosity.Quiet).Confirm(Confirmation(autoApprove: true)));

        // Assert
        _out.Output.ShouldBeEmpty();
        _error.Output.ShouldBeEmpty();
    }

    [Fact]
    public void Confirm_Proceeds_WhenUserTypesYes()
    {
        // Arrange
        _out.Interactive();
        _out.Input.PushTextWithEnter("yes");

        // Act / Assert
        Should.NotThrow(() => _sut.Confirm(Confirmation(autoApprove: false)));
    }

    [Fact]
    public void Confirm_Throws_WhenUserTypesAnythingElse()
    {
        // Arrange — declining at the prompt is a non-zero exit, so a wrapping script can't mistake "no" for success.
        _out.Interactive();
        _out.Input.PushTextWithEnter("no");

        // Act / Assert
        Should.Throw<ConfirmationDeclinedException>(() => _sut.Confirm(Confirmation(autoApprove: false)));
    }

    [Fact]
    public void Confirm_PresentsTheSummary_BeforePrompting()
    {
        // Arrange
        _out.Interactive();
        _out.Input.PushTextWithEnter("yes");

        // Act
        _sut.Confirm(Confirmation(autoApprove: false));

        // Assert
        _out.Output.ShouldContain("execute 3 statement(s)");
    }

    [Fact]
    public void Confirm_QuietVerbosity_StillPromptsWithTheSummary()
    {
        // Arrange — interaction is never suppressed: whatever the verbosity, the operator sees what they approve.
        _out.Interactive();
        _out.Input.PushTextWithEnter("yes");

        // Act
        Build(Verbosity.Quiet).Confirm(Confirmation(autoApprove: false));

        // Assert
        _out.Output.ShouldContain("execute 3 statement(s)");
        _out.Output.ShouldContain("Only yes will be accepted");
    }

    [Fact]
    public void Confirm_Throws_WhenNotInteractive()
    {
        // Arrange — a non-interactive console (redirected stdin / CI / a container) has no input to read. Declining
        // silently would exit 0 and look like a successful no-op, so it must fail loudly instead.

        // Act / Assert
        var ex = Should.Throw<ConfirmationDeclinedException>(() => _sut.Confirm(Confirmation(autoApprove: false)));
        ex.Message.ShouldContain("no interactive terminal");
        ex.Message.ShouldContain("--auto-approve");
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
        var info = new StateLockInfo(new LockId("abc123"), "apply", new LockHolder("tom@dev"), DateTimeOffset.UnixEpoch, ExpiresUtc: null);

        _sut.ReportLockInfo(info);

        // Just the lock's data, as detail lines on stdout — no headline narrative (that's the command's).
        _out.Output.ShouldContain("abc123");
        _out.Output.ShouldContain("tom@dev");
        _out.Output.ShouldContain("apply");
        _error.Output.ShouldBeEmpty();
    }

    [Fact]
    public void ReportLockInfo_QuietVerbosity_StillWritesTheLock()
    {
        // A single-object artifact has one face: its full rendering already is the summary.
        var info = new StateLockInfo(new LockId("abc123"), "apply", new LockHolder("tom@dev"), DateTimeOffset.UnixEpoch, ExpiresUtc: null);

        Build(Verbosity.Quiet).ReportLockInfo(info);

        _out.Output.ShouldContain("abc123");
    }

    [Fact]
    public void ReportScriptHashes_Empty_WritesNoDeclarationsMessage()
    {
        _sut.ReportScriptHashes([]);

        _out.Output.ShouldContain("No scripts are declared");
        _error.Output.ShouldBeEmpty();
    }

    [Fact]
    public void ReportScriptHashes_WritesTheDeclarationTableToOutput()
    {
        _sut.ReportScriptHashes([new ScriptHashEntry("seed-users", "abc123")]);

        _out.Output.ShouldContain("seed-users");
        _out.Output.ShouldContain("abc123");
        _error.Output.ShouldBeEmpty();
    }

    [Fact]
    public void ReportScriptHashes_QuietVerbosity_WritesTheCount()
    {
        Build(Verbosity.Quiet).ReportScriptHashes([new ScriptHashEntry("seed-users", "abc123"), new ScriptHashEntry("backfill", "def456")]);

        _out.Output.ShouldContain("2 scripts declared.");
        _out.Output.ShouldNotContain("abc123");
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
        _sut.ReportProjectPlugins([new ProjectPlugin("provider", "postgres", "NSchema.Postgres", SemanticVersion.Parse("4.0.0"), true, "/c")]);

        // Assert
        _out.Output.ShouldContain("postgres");
        _out.Output.ShouldContain("NSchema.Postgres");
        _out.Output.ShouldContain("4.0.0");
    }

    [Fact]
    public void ReportProjectPlugins_QuietVerbosity_WritesTheCounts()
    {
        // Act
        Build(Verbosity.Quiet).ReportProjectPlugins(
        [
            new ProjectPlugin("provider", "postgres", "NSchema.Postgres", SemanticVersion.Parse("4.0.0"), true, "/c"),
            new ProjectPlugin("backend", "s3", "NSchema.Aws", SemanticVersion.Parse("4.1.0"), false, null),
        ]);

        // Assert
        _out.Output.ShouldContain("2 plugins configured, 1 not restored.");
        _out.Output.ShouldNotContain("NSchema.Postgres");
    }

    [Fact]
    public void ReportOutdatedPlugins_Empty_WritesNoPluginsMessage()
    {
        // Act
        _sut.ReportOutdatedPlugins([]);

        // Assert
        _out.Output.ShouldContain("No provider or backend plugins");
    }

    [Fact]
    public void ReportOutdatedPlugins_WritesCurrentWantedLatestAndUpdateHint()
    {
        // Act
        _sut.ReportOutdatedPlugins([new OutdatedPlugin("provider", "postgres", "NSchema.Postgres", SemanticVersion.Parse("5.0.0"), SemanticVersion.Parse("5.2.0"), SemanticVersion.Parse("5.2.0"), Outdated: true)]);

        // Assert
        _out.Output.ShouldContain("postgres");
        _out.Output.ShouldContain("5.0.0");
        _out.Output.ShouldContain("5.2.0");
        _out.Output.ShouldContain("plugin update");
    }

    [Fact]
    public void ReportOutdatedPlugins_AllCurrent_WritesUpToDate()
    {
        // Act
        _sut.ReportOutdatedPlugins([new OutdatedPlugin("provider", "postgres", "NSchema.Postgres", SemanticVersion.Parse("5.2.0"), SemanticVersion.Parse("5.2.0"), SemanticVersion.Parse("5.2.0"), Outdated: false)]);

        // Assert
        _out.Output.ShouldContain("up to date");
    }

    [Fact]
    public void ReportOutdatedPlugins_QuietVerbosity_WritesTheCounts()
    {
        // Act
        Build(Verbosity.Quiet).ReportOutdatedPlugins([new OutdatedPlugin("provider", "postgres", "NSchema.Postgres", SemanticVersion.Parse("5.0.0"), SemanticVersion.Parse("5.2.0"), SemanticVersion.Parse("5.2.0"), Outdated: true)]);

        // Assert
        _out.Output.ShouldContain("1 of 1 plugins outdated.");
        _out.Output.ShouldNotContain("NSchema.Postgres");
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
        _sut.ReportCachedPlugins("/cache/root", [new CachedPlugin("NSchema.Postgres", SemanticVersion.Parse("4.0.0"), "/c", 2 * 1024 * 1024)]);

        // Assert
        _out.Output.ShouldContain("NSchema.Postgres");
        _out.Output.ShouldContain("4.0.0");
        _out.Output.ShouldContain("MiB");
    }

    [Fact]
    public void ReportCachedPlugins_QuietVerbosity_WritesTheCountAndSize()
    {
        // Act
        Build(Verbosity.Quiet).ReportCachedPlugins("/cache/root", [new CachedPlugin("NSchema.Postgres", SemanticVersion.Parse("4.0.0"), "/c", 2 * 1024 * 1024)]);

        // Assert
        _out.Output.ShouldContain("/cache/root");
        _out.Output.ShouldContain("1 cached, 2.0 MiB total.");
        _out.Output.ShouldNotContain("NSchema.Postgres");
    }

    [Fact]
    public void ReportPluginDetail_NotRestored_HintsToRunInit()
    {
        // Act
        _sut.ReportPluginDetail(new ProjectPlugin("backend", "s3", "NSchema.Aws", SemanticVersion.Parse("4.0.0"), false, null));

        // Assert
        _out.Output.ShouldContain("s3");
        _out.Output.ShouldContain("NSchema.Aws");
        _out.Output.ShouldContain("init");
    }

    [Fact]
    public void ReportPluginDetail_QuietVerbosity_StillWritesTheDetail()
    {
        // A single-object artifact has one face: its full rendering already is the summary.
        Build(Verbosity.Quiet).ReportPluginDetail(new ProjectPlugin("backend", "s3", "NSchema.Aws", SemanticVersion.Parse("4.0.0"), false, null));

        _out.Output.ShouldContain("NSchema.Aws");
    }

    [Fact]
    public void ReportException_WritesMessageToErrorConsole()
    {
        // Arrange
        var ex = new Exception("Boom!");

        // Act
        _sut.ReportException(ex);

        // Assert
        _error.Output.ShouldContain("Boom!");
        _out.Output.ShouldBeEmpty();
    }

    [Fact]
    public void ReportException_NamesTheTypeAndAsksForABugReport()
    {
        // Arrange
        // A NullReferenceException's message locates nothing on its own, which is the whole reason the unexpected
        // path prints the type, the report link, and the stack rather than the message alone.
        var ex = Caught(() => _ = ((string)null!).Length);

        // Act
        _sut.ReportException(ex);

        // Assert
        _error.Output.ShouldContain("Internal error");
        _error.Output.ShouldContain(nameof(NullReferenceException));
        _error.Output.ShouldContain(ExceptionReport.IssuesUrl);
        _error.Output.ShouldContain(nameof(ReportException_NamesTheTypeAndAsksForABugReport));
    }

    [Fact]
    public void ReportException_QuietVerbosity_IsNeverSuppressed()
    {
        // Act
        Build(Verbosity.Quiet).ReportException(new Exception("Boom!"));

        // Assert
        _error.Output.ShouldContain("Boom!");
    }

    // Throwing for real gives the exception a populated stack; `new NullReferenceException()` would have none.
    private static Exception Caught(Action act)
    {
        try
        {
            act();
            throw new InvalidOperationException("Expected the action to throw.");
        }
        catch (Exception ex)
        {
            return ex;
        }
    }

    [Fact]
    public void ReportDiagnostics_WritesPlaceholder_WhenEmpty()
    {
        // Act
        _sut.ReportDiagnostics([]);

        // Assert
        _out.Output.ShouldContain("No diagnostics.");
    }

    [Fact]
    public void ReportDiagnostics_WritesInfoToOutput()
    {
        // Arrange
        var diagnostics = (Diagnostic[])[new Diagnostic("schema-lint", "naming-hint", "Naming hint", DiagnosticSeverity.Info)];

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
        var diagnostics = (Diagnostic[])
        [
            new Diagnostic("destructive-actions", "destructive-change", "Dropping column id", DiagnosticSeverity.Error),
        ];

        // Act
        _sut.ReportDiagnostics(diagnostics);

        // Assert
        _error.Output.ShouldContain("Dropping column id");
        _error.Output.ShouldContain("destructive-actions");
        _out.Output.ShouldBeEmpty();
    }

    [Fact]
    public void ReportDiagnostics_QuietVerbosity_DropsInfoRows()
    {
        // Arrange — Info rows (a run-once skip on every plan, say) are narration-grade; the actionable rows survive.
        var diagnostics = (Diagnostic[])
        [
            new Diagnostic("run-once", "run-once", "Skipping 'seed-users': already executed.", DiagnosticSeverity.Info),
            new Diagnostic("drift", "drift-detected", "Recorded state differs from the live database.", DiagnosticSeverity.Warning),
        ];

        // Act
        Build(Verbosity.Quiet).ReportDiagnostics(diagnostics);

        // Assert
        _error.Output.ShouldContain("Recorded state differs");
        _error.Output.ShouldNotContain("Skipping");
    }

    [Fact]
    public void ReportDiagnostics_QuietVerbosity_AllInfo_RendersNothing()
    {
        // Act
        Build(Verbosity.Quiet).ReportDiagnostics([new Diagnostic("run-once", "run-once", "Skipping 'seed-users'.", DiagnosticSeverity.Info)]);

        // Assert
        _out.Output.ShouldBeEmpty();
        _error.Output.ShouldBeEmpty();
    }

    // A diff renaming nothing but adding one schema with a table whose column changes type to an array — the `[]`
    // exercises the reporter's markup escaping, and the added schema/table gives the framing tests real content.
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
    public void ReportDiff_QuietVerbosity_WritesTheCountsLine()
    {
        // Act
        Build(Verbosity.Quiet).ReportDiff(new DatabaseDiff([SchemaDiff.Added("app")]));

        // Assert — one line in place of the full rendering.
        _out.Output.ShouldContain("Plan: 1 to add, 0 to change, 0 to destroy.");
        _out.Output.ShouldNotContain("+ schema app");
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
    public void ReportPlan_QuietVerbosity_WritesTheCountsAndStatementLine()
    {
        // Arrange
        var plan = new MigrationPlan(new DatabaseDiff([SchemaDiff.Added("app")]),
        [
            new SqlStatement("CREATE SCHEMA app", RunOutsideTransaction: false),
            new SqlStatement("COMMENT ON SCHEMA app IS ''", RunOutsideTransaction: false),
        ]);

        // Act — the whole point of quiet mode: hundreds of CI runs summarize in one line each, not a full dump.
        Build(Verbosity.Quiet).ReportPlan(plan);

        // Assert
        _out.Output.ShouldContain("Plan: 1 to add, 0 to change, 0 to destroy (2 statements).");
        _out.Output.ShouldNotContain("+ schema app");
        _out.Output.ShouldNotContain("CREATE SCHEMA");
    }

    [Fact]
    public void ReportPlan_QuietVerbosity_CountsAdoptions()
    {
        // Arrange
        var plan = new MigrationPlan(new DatabaseDiff(), [])
        {
            Adopted = new IdentitySet(SchemaObjects: [ObjectAddress.Table("app", "users")]),
        };

        // Act
        Build(Verbosity.Quiet).ReportPlan(plan);

        // Assert
        _out.Output.ShouldContain("Plan: 0 to add, 0 to change, 0 to destroy, 1 to adopt (0 statements).");
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

    [Fact]
    public void ReportDatabase_QuietVerbosity_WritesTheObjectCounts()
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

        // Act — asking for the schema and asking for quiet means the counts; the total rule has no per-command carve-outs.
        Build(Verbosity.Quiet).ReportDatabase(database);

        // Assert
        _out.Output.ShouldContain("Database: 1 schema, 1 table.");
        _out.Output.ShouldNotContain("widgets");
    }

    // The ANSI escape for [dim], which a console emitting its styling writes ahead of the dimmed text.
    private const string Dim = "[2m";

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
    public void ReportState_QuietVerbosity_WritesTheCounts()
    {
        // Arrange
        var state = PartlyManagedState() with
        {
            Scripts = [new ScriptExecution(new ScriptReference(null, "seed-users"), new ScriptHash("abc123"), DateTimeOffset.UnixEpoch)],
        };

        // Act
        Build(Verbosity.Quiet).ReportState(state);

        // Assert
        _out.Output.ShouldContain("State: 1 schema, 2 tables. 2 managed objects, 1 recorded script execution.");
        _out.Output.ShouldNotContain("legacy_audit");
    }

    [Fact]
    public void ReportState_DimsTheWholeUnmanagedBlock()
    {
        // Arrange — a console that emits its styling, so what is dimmed can be asserted rather than inferred.
        var console = new TestConsole().EmitAnsiSequences();
        console.Profile.Width = 200;
        var sut = new SpectreConsoleReporter(console, new TestConsole(), Verbosity.Normal);

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
    public void ReportScripts_QuietVerbosity_WritesTheCount()
    {
        // Act
        Build(Verbosity.Quiet).ReportScripts([new ScriptExecution(new ScriptReference(null, "seed-users"), new ScriptHash("abc123"), DateTimeOffset.UnixEpoch)]);

        // Assert
        _out.Output.ShouldContain("1 script execution recorded.");
        _out.Output.ShouldNotContain("seed-users");
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
