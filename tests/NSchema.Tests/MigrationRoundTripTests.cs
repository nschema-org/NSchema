using System.CommandLine;
using NSchema.Configuration;
using NSchema.Tests.Fixtures;
using Spectre.Console;
using RootCommand = NSchema.Commands.RootCommand;

namespace NSchema.Tests;

/// <summary>
/// Drives the real CLI pipeline (parse → configure → run) against a live PostgreSQL container to prove the
/// plan/apply/refresh/destroy/plan --destroy/show/drift happy paths end to end. Each test uses a unique database
/// schema for isolation.
/// </summary>
[Collection("postgres")]
public sealed class MigrationRoundTripTests(PostgresContainerFixture fixture) : IAsyncLifetime
{
    private readonly string _schema = $"test_{Guid.NewGuid():N}";
    private readonly string _schemaDirectory = Directory.CreateTempSubdirectory("nschema-int-").FullName;

    public ValueTask InitializeAsync()
    {
        // A desired schema describing one table in this test's unique schema, written as NSchema DDL — the only
        // schema format. The desired schema is every *.sql file found recursively under the project directory.
        var schemaDocument = $"""
        CREATE SCHEMA {_schema};

        CREATE TABLE {_schema}.widgets (
          id   bigint NOT NULL,
          name text,
          CONSTRAINT widgets_pkey PRIMARY KEY (id)
        );
        """;

        File.WriteAllText(Path.Combine(_schemaDirectory, "schema.sql"), schemaDocument);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await using var command = fixture.DataSource.CreateCommand($"DROP SCHEMA IF EXISTS \"{_schema}\" CASCADE");
        await command.ExecuteNonQueryAsync();
        Directory.Delete(_schemaDirectory, recursive: true);
    }

    [Fact]
    public async Task Apply_CreatesTheDesiredSchemaInTheDatabase()
    {
        // Act
        var (exitCode, _) = await RunCli("apply", "--auto-approve");

        // Assert
        exitCode.ShouldBe(0);
        (await TableExists("widgets")).ShouldBeTrue();
    }

    [Fact]
    public async Task Plan_DoesNotModifyTheDatabase()
    {
        // Act
        var (exitCode, _) = await RunCli("plan");

        // Assert
        exitCode.ShouldBe(0);
        (await TableExists("widgets")).ShouldBeFalse();
    }

    [Fact]
    public async Task Refresh_WritesTheLiveSchemaToTheStateFile()
    {
        // Arrange
        // Refresh reads the live database, so it needs no desired-schema options. The state store is declared in a
        // BACKEND config block in the project's .sql files.
        await RunCli("apply", "--auto-approve");
        var stateFile = Path.Combine(_schemaDirectory, "state.json");
        WriteStateConfig("state.json");

        // Act
        var (exitCode, _) = await RunCli("refresh");

        // Assert
        exitCode.ShouldBe(0);
        File.Exists(stateFile).ShouldBeTrue();
        (await File.ReadAllTextAsync(stateFile, TestContext.Current.CancellationToken)).ShouldContain("widgets");
    }

    [Fact]
    public async Task Destroy_DropsTheManagedSchemaFromTheDatabase()
    {
        // Arrange — create the table so there's something to tear down. With no state store, destroy reads the
        // managed schema from the desired-schema files and drops everything declared there.
        await RunCli("apply", "--auto-approve");
        (await TableExists("widgets")).ShouldBeTrue();

        // Act
        var (exitCode, _) = await RunCli("destroy", "--auto-approve");

        // Assert
        exitCode.ShouldBe(0);
        (await TableExists("widgets")).ShouldBeFalse();
    }

    [Fact]
    public async Task PlanDestroy_PreviewsTheTeardownWithoutDropping()
    {
        // Arrange — a table to tear down. With no state store, plan --destroy reads the managed schema from the
        // desired-schema files and renders the teardown SQL against the live database, but never executes it.
        await RunCli("apply", "--auto-approve");

        // Act
        var (exitCode, _) = await RunCli("plan", "--destroy");

        // Assert — the teardown is only previewed: the command succeeds, but the table is left in place.
        exitCode.ShouldBe(0);
        (await TableExists("widgets")).ShouldBeTrue();
    }

    [Fact]
    public async Task Show_PrintsTheRecordedState()
    {
        // Arrange — apply, then refresh to record the live schema into the state store.
        await RunCli("apply", "--auto-approve");
        WriteStateConfig("state.json");
        await RunCli("refresh");

        // Act — show reads only the recorded state (it never contacts the live database).
        var (exitCode, output) = await RunCli("show");

        // Assert
        exitCode.ShouldBe(0);
        output.ShouldContain("widgets");
    }

    [Fact]
    public async Task Drift_DetectsAnOutOfBandChange()
    {
        // Arrange — record state matching the live schema, then change the live database behind NSchema's back.
        await RunCli("apply", "--auto-approve");
        WriteStateConfig("state.json");
        await RunCli("refresh");
        await ExecuteSql($"ALTER TABLE \"{_schema}\".widgets DROP COLUMN name");

        // Act — drift compares the recorded state (which still has the column) against the live database.
        var (exitCode, output) = await RunCli("drift");

        // Assert — drift is a pure observation, so it succeeds, and it reports the dropped column.
        exitCode.ShouldBe(0);
        output.ShouldContain("name");
    }

    [Fact]
    public async Task Apply_ThenPlan_WithUniqueAndCheckConstraints_RoundTripsCleanly()
    {
        // Arrange — a schema exercising unique and check constraints (with constraint comments), which the Postgres
        // provider both generates (as separate ALTER TABLE ADD CONSTRAINT statements) and introspects. Applying then
        // re-planning must round-trip cleanly: what we read back out of the database has to match what we declared,
        // or the second plan would show spurious changes. The `integer` spelling exercises the DDL's alias to the
        // canonical `int`, which introspection reports — without it, the re-plan would drift on the column type.
        var schemaDocument = $"""
        CREATE SCHEMA {_schema};

        CREATE TABLE {_schema}.accounts (
          id      bigint  NOT NULL,
          email   text    NOT NULL,
          balance integer NOT NULL,
          CONSTRAINT accounts_pkey PRIMARY KEY (id),
          --- one account per email address
          CONSTRAINT accounts_email_key UNIQUE (email),
          --- balances may never go negative
          CONSTRAINT accounts_balance_check CHECK (balance >= 0)
        );
        """;
        File.WriteAllText(Path.Combine(_schemaDirectory, "schema.sql"), schemaDocument);

        // Act
        var (applyExit, _) = await RunCli("apply", "--auto-approve");
        var (planExit, planOutput) = await RunCli("plan");

        // Assert — the apply succeeds, both constraints land with their comments, and the re-plan finds nothing to do.
        applyExit.ShouldBe(0);
        (await ConstraintComment("accounts_email_key")).ShouldBe("one account per email address");
        (await ConstraintComment("accounts_balance_check")).ShouldBe("balances may never go negative");
        planExit.ShouldBe(0);
        planOutput.ShouldContain("No changes detected.");
    }

    private async Task<string?> ConstraintComment(string constraintName)
    {
        await using var command = fixture.DataSource.CreateCommand(
            "SELECT obj_description(oid, 'pg_constraint') FROM pg_constraint WHERE conname = @name AND connamespace = @schema::regnamespace");
        command.Parameters.AddWithValue("name", constraintName);
        command.Parameters.AddWithValue("schema", _schema);
        return await command.ExecuteScalarAsync() as string;
    }

    private async Task<(int ExitCode, string Output)> RunCli(string command, params string[] commandArguments)
    {
        // --directory points every command at the project dir; the .sql files and their relative paths resolve there.
        string[] args = [command, "--directory", _schemaDirectory, .. commandArguments];

        // The live database is supplied the way real usage does it: via the connection-string environment variable.
        // --directory changes the process working directory (in ConfigurationFactory), so snapshot and restore it.
        var workingDirectory = Directory.GetCurrentDirectory();
        Environment.SetEnvironmentVariable(EnvironmentVariables.PostgresConnectionString, fixture.ConnectionString);

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
            Environment.SetEnvironmentVariable(EnvironmentVariables.PostgresConnectionString, null);
        }
    }

    // Declares a file state backend in a config.sql, so the state-aware commands (refresh/show/drift) pick it up the
    // way real usage does — config blocks alongside the schema, no per-command flags.
    private void WriteStateConfig(string stateFileName)
    {
        File.WriteAllText(Path.Combine(_schemaDirectory, "config.sql"), $"BACKEND file ( path = '{stateFileName}' );");
    }

    private async Task<bool> TableExists(string table)
    {
        await using var command = fixture.DataSource.CreateCommand(
            "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = @schema AND table_name = @table)");
        command.Parameters.AddWithValue("schema", _schema);
        command.Parameters.AddWithValue("table", table);
        return (bool)(await command.ExecuteScalarAsync())!;
    }

    private async Task ExecuteSql(string sql)
    {
        await using var command = fixture.DataSource.CreateCommand(sql);
        await command.ExecuteNonQueryAsync();
    }
}
