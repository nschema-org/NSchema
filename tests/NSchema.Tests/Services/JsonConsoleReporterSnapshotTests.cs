using NSchema.Configuration.Domain;
using NSchema.Configuration.Plugins;
using NSchema.Services.Reporting;
using NSchema.State.Locks;

namespace NSchema.Tests.Services;

/// <summary>
/// Snapshot coverage for <see cref="JsonConsoleReporter"/>: the narration script replayed at each verbosity
/// (pinning which log events each level emits, and the results-on-stdout / logs-on-stderr split), and each
/// structured renderer once.
/// </summary>
public sealed class JsonConsoleReporterSnapshotTests
{
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    private JsonConsoleReporter Build(Verbosity verbosity) => new(verbosity, _out, _error);

    /// <summary>
    /// One of every narration the CLI emits, so the per-verbosity snapshots record the complete gating rule.
    /// </summary>
    private static void PlayNarrationScript(JsonConsoleReporter reporter)
    {
        reporter.ReportEnvironment("staging");
        reporter.Announce($"Applying schema migration. Changes will be applied to the {"orders"} database.");
        reporter.Report(MessageKind.Progress, "Generating SQL...");
        reporter.Report(MessageKind.Verbose, "Resolved provider plugin NSchema.Postgres 5.0.0.");
        reporter.Detail($"The lock is held until you run: nschema lock release");
        reporter.Warn($"Drift detected: {"1 changed"}.");
        reporter.Success($"Apply complete. {"2 added"}.");
    }

    private Task VerifyStreams() => Verify($"── stdout ──\n{_out}\n── stderr ──\n{_error}");

    [Fact]
    public Task Narration_QuietVerbosity()
    {
        PlayNarrationScript(Build(Verbosity.Quiet));

        return VerifyStreams();
    }

    [Fact]
    public Task Narration_NormalVerbosity()
    {
        PlayNarrationScript(Build(Verbosity.Normal));

        return VerifyStreams();
    }

    [Fact]
    public Task Narration_VerboseVerbosity()
    {
        PlayNarrationScript(Build(Verbosity.Verbose));

        return VerifyStreams();
    }

    [Fact]
    public Task ReportDiagnostics_MixedSeverities()
    {
        Build(Verbosity.Normal).ReportDiagnostics(
        [
            new Diagnostic("run-once", "run-once", "Skipping 'seed-users': already executed.", DiagnosticSeverity.Info),
            new Diagnostic("drift", "drift-detected", "Recorded state differs from the live database.", DiagnosticSeverity.Warning),
            new Diagnostic("destructive-actions", "destructive-change", "Dropping column id", DiagnosticSeverity.Error),
        ]);

        return VerifyStreams();
    }

    [Fact]
    public Task ReportLockInfo_HeldWithExpiry()
    {
        var info = new StateLockInfo(new LockId("abc123"), "apply", new LockHolder("tom@dev"), DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(30));

        Build(Verbosity.Normal).ReportLockInfo(info);

        return VerifyStreams();
    }

    [Fact]
    public Task ReportLockInfo_NotHeld()
    {
        Build(Verbosity.Normal).ReportLockInfo(null);

        return VerifyStreams();
    }

    [Fact]
    public Task ReportScriptHashes_DeclaredScripts()
    {
        Build(Verbosity.Normal).ReportScriptHashes(
        [
            new ScriptHashEntry("seed-users", "abc123"),
            new ScriptHashEntry("backfill", "def456"),
        ]);

        return VerifyStreams();
    }

    [Fact]
    public Task ReportProjectPlugins_RestoredAndMissing()
    {
        Build(Verbosity.Normal).ReportProjectPlugins(
        [
            new ProjectPlugin("provider", "postgres", "NSchema.Postgres", SemanticVersion.Parse("4.0.0"), Restored: true, CachePath: "/c"),
            new ProjectPlugin("backend", "s3", "NSchema.Aws", SemanticVersion.Parse("4.1.0"), Restored: false, CachePath: null),
        ]);

        return VerifyStreams();
    }

    [Fact]
    public Task ReportCachedPlugins_SizedListing()
    {
        Build(Verbosity.Normal).ReportCachedPlugins("/cache/root",
        [
            new CachedPlugin("NSchema.Postgres", SemanticVersion.Parse("4.0.0"), "/c", 2 * 1024 * 1024),
        ]);

        return VerifyStreams();
    }

    [Fact]
    public Task ReportOutdatedPlugins_MixedCurrency()
    {
        Build(Verbosity.Normal).ReportOutdatedPlugins(
        [
            new OutdatedPlugin("provider", "postgres", "NSchema.Postgres", SemanticVersion.Parse("5.0.0"), SemanticVersion.Parse("5.2.0"), SemanticVersion.Parse("5.2.0"), Outdated: true),
        ]);

        return VerifyStreams();
    }
}
