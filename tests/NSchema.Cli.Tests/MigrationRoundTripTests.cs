using System.CommandLine;
using NSchema.Cli.Tests.Fixtures;
using CliCommands = NSchema.Cli.Commands;

namespace NSchema.Cli.Tests;

/// <summary>
/// Drives the real CLI pipeline (parse → configure → run) against a live PostgreSQL container to prove the
/// plan/apply/refresh happy paths end to end. Each test uses a unique database schema for isolation.
/// </summary>
[Collection("postgres")]
public sealed class MigrationRoundTripTests(PostgresContainerFixture fixture) : IAsyncLifetime
{
    private readonly string _schema = $"test_{Guid.NewGuid():N}";
    private readonly string _schemaDirectory = Directory.CreateTempSubdirectory("nschema-int-").FullName;

    public Task InitializeAsync()
    {
        // A desired schema describing one table in this test's unique schema.
        var schemaDocument = $$"""
        {
          "schemas": [
            {
              "name": "{{_schema}}",
              "tables": [
                {
                  "name": "widgets",
                  "primaryKey": { "name": "widgets_pkey", "columnNames": ["id"] },
                  "columns": [
                    { "name": "id", "type": "bigint", "isNullable": false },
                    { "name": "name", "type": "text", "isNullable": true }
                  ]
                }
              ]
            }
          ]
        }
        """;

        File.WriteAllText(Path.Combine(_schemaDirectory, "schema.json"), schemaDocument);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await using var command = fixture.DataSource.CreateCommand($"DROP SCHEMA IF EXISTS \"{_schema}\" CASCADE");
        await command.ExecuteNonQueryAsync();
        Directory.Delete(_schemaDirectory, recursive: true);
    }

    [Fact]
    public async Task Apply_CreatesTheDesiredSchemaInTheDatabase()
    {
        // Act
        var exitCode = await RunCli("apply", [.. SchemaArguments, "--auto-approve"]);

        // Assert
        exitCode.ShouldBe(0);
        (await TableExists("widgets")).ShouldBeTrue();
    }

    [Fact]
    public async Task Plan_DoesNotModifyTheDatabase()
    {
        // Act
        var exitCode = await RunCli("plan", SchemaArguments);

        // Assert
        exitCode.ShouldBe(0);
        (await TableExists("widgets")).ShouldBeFalse();
    }

    [Fact]
    public async Task Refresh_WritesTheLiveSchemaToTheStateFile()
    {
        // Arrange
        // Refresh reads the live database, so it needs no desired-schema options.
        await RunCli("apply", [.. SchemaArguments, "--auto-approve"]);
        var stateFile = Path.Combine(_schemaDirectory, "state.json");

        // Act
        var exitCode = await RunCli("refresh", ["--state-file", stateFile]);

        // Assert
        exitCode.ShouldBe(0);
        File.Exists(stateFile).ShouldBeTrue();
        (await File.ReadAllTextAsync(stateFile)).ShouldContain("widgets");
    }

    // Desired-schema options are valid only for plan and apply; refresh rejects them.
    private string[] SchemaArguments => ["--schema-dir", _schemaDirectory, "--format", "json"];

    private async Task<int> RunCli(string command, string[] commandArguments)
    {
        string[] args =
        [
            command,
            "--provider", "postgres",
            "--connection-string", fixture.ConnectionString,
            .. commandArguments,
        ];

        // Disable the built-in handler so a failure surfaces as a thrown exception in the test rather than exit code 1.
        return await CliCommands.RootCommand.Create()
            .Parse(args)
            .InvokeAsync(new InvocationConfiguration { EnableDefaultExceptionHandler = false });
    }

    private async Task<bool> TableExists(string table)
    {
        await using var command = fixture.DataSource.CreateCommand(
            "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = @schema AND table_name = @table)");
        command.Parameters.AddWithValue("schema", _schema);
        command.Parameters.AddWithValue("table", table);
        return (bool)(await command.ExecuteScalarAsync())!;
    }
}
