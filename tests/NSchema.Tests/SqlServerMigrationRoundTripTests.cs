using System.CommandLine;
using NSchema.Commands;
using NSchema.Configuration;
using NSchema.Tests.Fixtures;
using Spectre.Console;
using RootCommand = NSchema.Commands.RootCommand;

namespace NSchema.Tests;

/// <summary>
/// Drives the real CLI pipeline (parse → configure → run) against a live SQL Server container to prove the SQL Server
/// provider is wired into the CLI end to end. Mirrors <see cref="PostgresRoundTripTests"/> (Postgres) but against SQL
/// Server; each test uses a unique schema for isolation.
/// </summary>
[Collection("sqlserver")]
public sealed class SqlServerMigrationRoundTripTests(SqlServerContainerFixture fixture) : IAsyncLifetime
{
    private readonly string _schema = $"test_{Guid.NewGuid():N}";
    private readonly string _schemaDirectory = Directory.CreateTempSubdirectory("nschema-mssql-").FullName;

    public ValueTask InitializeAsync()
    {
        // A desired schema describing one table in this test's unique schema, written as NSchema DDL — the only schema
        // format. `int` / `varchar` are the canonical types the SQL Server provider introspects back out.
        var schemaDocument = $"""
        CREATE SCHEMA {_schema};

        CREATE TABLE {_schema}.widgets (
          id   int NOT NULL,
          name varchar(100),
          CONSTRAINT widgets_pkey PRIMARY KEY (id)
        );
        """;

        File.WriteAllText(Path.Combine(_schemaDirectory, "schema.sql"), schemaDocument);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await using (var connection = fixture.OpenConnection())
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"DROP TABLE IF EXISTS [{_schema}].[widgets]; DROP SCHEMA IF EXISTS [{_schema}];";
            await command.ExecuteNonQueryAsync();
        }

        Directory.Delete(_schemaDirectory, recursive: true);
    }

    [Fact]
    public async Task Apply_ThenPlan_CreatesTheTableAndRoundTripsCleanly()
    {
        // Act — apply creates the table in a fresh schema, then a re-plan introspects it back out.
        var (applyExit, _) = await RunCli("apply", "--auto-approve");
        var (planExit, planOutput) = await RunCli("plan");

        // Assert — apply succeeds and the re-plan finds nothing to do: what the provider read back out of SQL Server
        // has to match what was declared, or this second plan would show spurious changes.
        applyExit.ShouldBe(ExitCodes.NoChanges);
        (await TableExists("widgets")).ShouldBeTrue();
        planExit.ShouldBe(ExitCodes.NoChanges);
        planOutput.ShouldContain("No changes detected.");
    }

    [Fact]
    public async Task Plan_WithDetailedExitCode_SignalsPendingChangesWithoutModifyingTheDatabase()
    {
        // Act — the empty database differs from the desired schema.
        var (exitCode, _) = await RunCli("plan", "--detailed-exitcode");

        // Assert — with --detailed-exitcode plan signals pending changes (2) without touching the database.
        exitCode.ShouldBe(ExitCodes.HasChanges);
        (await TableExists("widgets")).ShouldBeFalse();
    }

    private async Task<(int ExitCode, string Output)> RunCli(string command, params string[] commandArguments)
    {
        // --directory points every command at the project dir; the .sql files and their relative paths resolve there.
        string[] args = [command, "--directory", _schemaDirectory, .. commandArguments];

        // The live database is supplied the way real usage does it: via the connection-string environment variable.
        // --directory changes the process working directory (in ConfigurationFactory), so snapshot and restore it.
        var workingDirectory = Directory.GetCurrentDirectory();
        Environment.SetEnvironmentVariable(EnvironmentVariables.SqlServerConnectionString, fixture.ConnectionString);

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
            Environment.SetEnvironmentVariable(EnvironmentVariables.SqlServerConnectionString, null);
        }
    }

    private async Task<bool> TableExists(string table)
    {
        await using var connection = fixture.OpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT CASE WHEN EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = @schema AND table_name = @table) THEN 1 ELSE 0 END";
        command.Parameters.AddWithValue("@schema", _schema);
        command.Parameters.AddWithValue("@table", table);
        return (int)(await command.ExecuteScalarAsync())! == 1;
    }
}
