using System.CommandLine;
using NSchema.Commands;
using NSchema.Tests.Fixtures;
using Spectre.Console;
using RootCommand = NSchema.Commands.RootCommand;

namespace NSchema.Tests;

/// <summary>
/// Drives the real CLI pipeline (apply → plan → evolve → refresh → drift → destroy) against a live database to prove a
/// provider is wired in end to end. The scenario and CLI plumbing live here; subclasses supply only the provider
/// specifics — connection, schema DDL, and verification queries — so every provider runs the same suite.
/// </summary>
/// <remarks>
/// The provider is configured purely through the connection-string environment variable (it is self-identifying), so
/// the apply/plan/destroy commands run with <em>no</em> state backend — they diff the desired schema against the live
/// database. A state backend turns planning state-based, so it is introduced only for the state commands
/// (refresh/show/drift), parameterized over the backend under test.
/// </remarks>
public abstract class ProviderRoundTripTests : IAsyncLifetime
{
    /// <summary>The project directory the CLI runs against; recreated fresh per test, holding only <c>schema.sql</c>.</summary>
    private protected string ProjectDirectory { get; private set; } = null!;

    // --- provider specifics the subclass supplies --------------------------------------------------------------------

    /// <summary>The environment variable the connection string is supplied through, e.g. <c>NSCHEMA_POSTGRES_CONNECTION_STRING</c>.</summary>
    protected abstract string ConnectionEnvironmentVariable { get; }

    /// <summary>The live database connection string.</summary>
    protected abstract string ConnectionString { get; }

    /// <summary>NSchema DDL for the initial desired schema: one <c>widgets</c> table with <c>id</c> and <c>name</c>.</summary>
    protected abstract string InitialSchemaDdl { get; }

    /// <summary>NSchema DDL evolving <c>widgets</c> with an added nullable <c>quantity</c> column.</summary>
    protected abstract string EvolvedSchemaDdl { get; }

    /// <summary>The provider-specific SQL that drops the <c>name</c> column out of band (for the drift scenario).</summary>
    protected abstract string DropNameColumnSql { get; }

    /// <summary>Whether the given table exists in this test's schema in the live database.</summary>
    protected abstract Task<bool> TableExists(string table);

    /// <summary>Executes raw SQL against the live database (used to make out-of-band changes).</summary>
    protected abstract Task ExecuteSql(string sql);

    /// <summary>Drops the test's schema/objects from the live database. The temp project directory is always removed.</summary>
    protected virtual Task CleanupDatabase() => Task.CompletedTask;

    /// <summary>The shared MinIO (S3-compatible) container backing the <c>s3</c> state-store tests.</summary>
    protected abstract MinioFixture Minio { get; }

    // Environment variables set for an s3-backend test (the ambient AWS configuration), cleared on dispose.
    private readonly List<string> _ambientAwsVariables = [];

    public async ValueTask InitializeAsync()
    {
        ProjectDirectory = Directory.CreateTempSubdirectory("nschema-rt-").FullName;
        await WriteSchema(InitialSchemaDdl);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var variable in _ambientAwsVariables)
        {
            Environment.SetEnvironmentVariable(variable, null);
        }

        await CleanupDatabase();
        Directory.Delete(ProjectDirectory, recursive: true);
    }

    // --- shared scenario: apply / plan / evolve / destroy (no state backend) -----------------------------------------

    [Fact]
    public async Task Apply_CreatesTheSchema_AndRePlanFindsNoChanges()
    {
        // Act — apply creates the table, then a re-plan introspects it back out.
        var (applyExit, _) = await RunCli("apply", "--auto-approve");
        var (planExit, planOutput) = await RunCli("plan");

        // Assert — apply succeeds and the re-plan finds nothing to do: what the provider read back out of the database
        // has to match what was declared, or this second plan would show spurious changes.
        applyExit.ShouldBe(ExitCodes.NoChanges);
        (await TableExists("widgets")).ShouldBeTrue();
        planExit.ShouldBe(ExitCodes.NoChanges);
        planOutput.ShouldContain("No changes detected.");
    }

    [Fact]
    public async Task Plan_WithDetailedExitCode_SignalsPendingChangesWithoutTouchingTheDatabase()
    {
        // Act — the empty database differs from the desired schema.
        var (exitCode, _) = await RunCli("plan", "--detailed-exitcode");

        // Assert — with --detailed-exitcode plan signals pending changes (2) without applying them.
        exitCode.ShouldBe(ExitCodes.HasChanges);
        (await TableExists("widgets")).ShouldBeFalse();
    }

    [Fact]
    public async Task Apply_EvolvingTheSchema_AppliesTheChange_AndRePlanIsClean()
    {
        // Arrange — apply the initial schema, then evolve it with an added column.
        await RunCli("apply", "--auto-approve");
        await WriteSchema(EvolvedSchemaDdl);

        // Act — applying the evolved schema adds the column; a re-plan must then introspect it back out cleanly.
        var (applyExit, _) = await RunCli("apply", "--auto-approve");
        var (planExit, planOutput) = await RunCli("plan");

        // Assert — the column lands and the re-plan finds nothing to do (proving the added column round-trips).
        applyExit.ShouldBe(ExitCodes.NoChanges);
        planExit.ShouldBe(ExitCodes.NoChanges);
        planOutput.ShouldContain("No changes detected.");
    }

    [Fact]
    public async Task Destroy_RemovesTheManagedSchema()
    {
        // Arrange — create the table so there's something to tear down.
        await RunCli("apply", "--auto-approve");
        (await TableExists("widgets")).ShouldBeTrue();

        // Act
        var (exitCode, _) = await RunCli("destroy", "--auto-approve");

        // Assert
        exitCode.ShouldBe(ExitCodes.NoChanges);
        (await TableExists("widgets")).ShouldBeFalse();
    }

    // --- state-backend scenario: refresh / show / drift (parameterized over the backend) -----------------------------

    [Theory]
    [InlineData("file")]
    [InlineData("s3")]
    public async Task Refresh_ThenShow_RecordsAndPrintsTheLiveSchema(string backend)
    {
        // Arrange — apply (live), then declare the state backend and refresh to record the live schema into it.
        await RunCli("apply", "--auto-approve");
        await ConfigureBackend(backend);
        var (refreshExit, _) = await RunCli("refresh");

        // Act — show reads only the recorded state (it never contacts the live database).
        var (showExit, output) = await RunCli("show");

        // Assert
        refreshExit.ShouldBe(ExitCodes.NoChanges);
        showExit.ShouldBe(ExitCodes.NoChanges);
        output.ShouldContain("widgets");
    }

    [Theory]
    [InlineData("file")]
    [InlineData("s3")]
    public async Task Drift_DetectsAnOutOfBandChange(string backend)
    {
        // Arrange — record state matching the live schema, then change the live database behind NSchema's back.
        await RunCli("apply", "--auto-approve");
        await ConfigureBackend(backend);
        await RunCli("refresh");
        await ExecuteSql(DropNameColumnSql);

        // Act — drift compares the recorded state (which still has the column) against the live database.
        var (exitCode, output) = await RunCli("drift", "--detailed-exitcode");

        // Assert — drift never modifies anything, but with --detailed-exitcode a detected divergence is signalled (2),
        // and the dropped column is reported.
        exitCode.ShouldBe(ExitCodes.HasChanges);
        output.ShouldContain("name");
    }

    // --- helpers -----------------------------------------------------------------------------------------------------

    /// <summary>
    /// Declares the state backend in a <c>config.sql</c> the state-aware commands pick up: a local <c>file</c>, or
    /// <c>s3</c> pointed at the shared MinIO container (endpoint/credentials supplied through the ambient AWS
    /// environment, exactly as real usage does — only path-style addressing rides on the block).
    /// </summary>
    private async Task ConfigureBackend(string backend)
    {
        switch (backend)
        {
            case "file":
                await WriteConfig("BACKEND file ( path = 'state.json' );");
                break;

            case "s3":
                SetAmbientAws("AWS_ENDPOINT_URL_S3", Minio.Endpoint);
                SetAmbientAws("AWS_ACCESS_KEY_ID", Minio.AccessKey);
                SetAmbientAws("AWS_SECRET_ACCESS_KEY", Minio.SecretKey);
                SetAmbientAws("AWS_REGION", "us-east-1");
                var key = $"state/{Guid.NewGuid():N}.json";
                await WriteConfig($"BACKEND s3 ( bucket = '{Minio.BucketName}', key = '{key}', force_path_style = true );");
                break;

            default:
                throw new ArgumentException($"Unsupported backend '{backend}'.", nameof(backend));
        }
    }

    private void SetAmbientAws(string variable, string value)
    {
        Environment.SetEnvironmentVariable(variable, value);
        _ambientAwsVariables.Add(variable);
    }

    private Task WriteConfig(string config) =>
        File.WriteAllTextAsync(Path.Combine(ProjectDirectory, "config.sql"), config, TestContext.Current.CancellationToken);

    /// <summary>Writes <paramref name="ddl"/> as the project's desired schema.</summary>
    private protected Task WriteSchema(string ddl) =>
        File.WriteAllTextAsync(Path.Combine(ProjectDirectory, "schema.sql"), ddl, TestContext.Current.CancellationToken);

    /// <summary>
    /// Runs one CLI command against <see cref="ProjectDirectory"/>, returning its exit code and captured output. The
    /// connection string is supplied via the environment variable, the working directory and console are snapshotted
    /// and restored, and the default exception handler is disabled so failures surface as thrown exceptions.
    /// </summary>
    private protected async Task<(int ExitCode, string Output)> RunCli(string command, params string[] commandArguments)
    {
        // --directory points every command at the project dir; the .sql files and their relative paths resolve there.
        string[] args = [command, "--directory", ProjectDirectory, .. commandArguments];

        // The live database is supplied the way real usage does it: via the connection-string environment variable.
        // --directory changes the process working directory (in ConfigurationFactory), so snapshot and restore it.
        var workingDirectory = Directory.GetCurrentDirectory();
        Environment.SetEnvironmentVariable(ConnectionEnvironmentVariable, ConnectionString);

        // Capture the CLI's output so command behaviour (the diff/schema/SQL the reporter prints) can be asserted.
        // The builder snapshots AnsiConsole.Console at construction, so swap it before the command builds its app.
        var writer = new StringWriter();
        var previousConsole = AnsiConsole.Console;
        var recording = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Out = new AnsiConsoleOutput(writer),
        });
        // A StringWriter has no terminal width, so Spectre defaults to 80 and elides framed content (the SQL preview)
        // to "...". Widen it so the rendered plan/diff/schema is captured in full.
        recording.Profile.Width = 500;
        AnsiConsole.Console = recording;
        try
        {
            var exitCode = await RootCommand.Create()
                .Parse(args)
                .InvokeAsync(new InvocationConfiguration { EnableDefaultExceptionHandler = false });
            return (exitCode, writer.ToString());
        }
        finally
        {
            AnsiConsole.Console = previousConsole;
            Directory.SetCurrentDirectory(workingDirectory);
            Environment.SetEnvironmentVariable(ConnectionEnvironmentVariable, null);
        }
    }
}
