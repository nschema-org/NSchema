using System.CommandLine;
using NSchema.Commands;
using NSchema.Configuration;
using Spectre.Console;
using RootCommand = NSchema.Commands.RootCommand;

namespace NSchema.Tests;

/// <summary>
/// Drives the real CLI pipeline (parse → configure → run) against a live SQLite database file to prove the SQLite
/// provider is wired in end to end. Unlike <see cref="PostgresRoundTripTests"/> this needs no Docker — SQLite is an
/// in-process file database — so it also exercises the native SQLite binary the provider loads.
/// </summary>
public sealed class SqliteMigrationRoundTripTests : IDisposable
{
    private readonly string _projectDirectory = Directory.CreateTempSubdirectory("nschema-sqlite-").FullName;
    private readonly string _databasePath;
    private readonly string _connectionString;

    public SqliteMigrationRoundTripTests()
    {
        _databasePath = Path.Combine(_projectDirectory, "app.db");
        _connectionString = $"Data Source={_databasePath}";

        // SQLite surfaces every object under the single 'main' schema, so the desired schema declares its table there
        // (no CREATE SCHEMA — 'main' always exists, and the provider rejects any other schema). Written as NSchema DDL,
        // the only schema format.
        var schemaDocument = """
        CREATE TABLE main.widgets (
          id   bigint NOT NULL,
          name text,
          CONSTRAINT widgets_pkey PRIMARY KEY (id)
        );
        """;

        File.WriteAllText(Path.Combine(_projectDirectory, "schema.sql"), schemaDocument);
    }

    public void Dispose() => Directory.Delete(_projectDirectory, recursive: true);

    [Fact]
    public async Task Apply_ThenPlan_CreatesTheTableAndRoundTripsCleanly()
    {
        // Act — apply creates the table in a fresh database, then a re-plan introspects it back out.
        var (applyExit, _) = await RunCli("apply", "--auto-approve");
        var (planExit, planOutput) = await RunCli("plan");

        // Assert — apply succeeds and the re-plan finds nothing to do: what the provider read back out of SQLite has
        // to match what was declared, or this second plan would show spurious changes.
        applyExit.ShouldBe(ExitCodes.NoChanges);
        File.Exists(_databasePath).ShouldBeTrue();
        planExit.ShouldBe(ExitCodes.NoChanges);
        planOutput.ShouldContain("No changes detected.");
    }

    [Fact]
    public async Task Plan_WithDetailedExitCode_SignalsPendingChangesAgainstAnEmptyDatabase()
    {
        // Act — the empty database differs from the desired schema.
        var (exitCode, _) = await RunCli("plan", "--detailed-exitcode");

        // Assert — with --detailed-exitcode plan signals pending changes (2) without applying them.
        exitCode.ShouldBe(ExitCodes.HasChanges);
    }

    private async Task<(int ExitCode, string Output)> RunCli(string command, params string[] commandArguments)
    {
        // --directory points every command at the project dir; the .sql files and their relative paths resolve there.
        string[] args = [command, "--directory", _projectDirectory, .. commandArguments];

        // The live database is supplied the way real usage does it: via the connection-string environment variable.
        // --directory changes the process working directory (in ConfigurationFactory), so snapshot and restore it.
        var workingDirectory = Directory.GetCurrentDirectory();
        Environment.SetEnvironmentVariable(EnvironmentVariables.SqliteConnectionString, _connectionString);

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
        recording.Profile.Width = 500;
        AnsiConsole.Console = recording;
        try
        {
            // Disable the built-in handler so a failure surfaces as a thrown exception in the test rather than exit code 1.
            var exitCode = await RootCommand.Create()
                .Parse(args)
                .InvokeAsync(new InvocationConfiguration { EnableDefaultExceptionHandler = false });
            return (exitCode, writer.ToString());
        }
        finally
        {
            AnsiConsole.Console = previousConsole;
            Directory.SetCurrentDirectory(workingDirectory);
            Environment.SetEnvironmentVariable(EnvironmentVariables.SqliteConnectionString, null);
        }
    }
}
