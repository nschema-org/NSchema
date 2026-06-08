using System.CommandLine;
using System.Text.Json;
using NSchema.Configuration;
using NSchema.Tests.Fixtures;
using RootCommand = NSchema.Commands.RootCommand;

namespace NSchema.Tests;

/// <summary>
/// Drives the real CLI pipeline (parse → configure → run) against a live PostgreSQL container to prove the
/// plan/apply/refresh happy paths end to end. Each test uses a unique database schema for isolation.
/// </summary>
[Collection("postgres")]
public sealed class MigrationRoundTripTests(PostgresContainerFixture fixture) : IAsyncLifetime
{
    private readonly string _schema = $"test_{Guid.NewGuid():N}";
    private readonly string _schemaDirectory = Directory.CreateTempSubdirectory("nschema-int-").FullName;

    public ValueTask InitializeAsync()
    {
        // A desired schema describing one table in this test's unique schema. YAML is the default format, so the
        // commands need no --format flag (the schema format is a config-only setting).
        var schemaDocument = $"""
        schemas:
          - name: {_schema}
            tables:
              - name: widgets
                primaryKey:
                  name: widgets_pkey
                  columnNames: [id]
                columns:
                  - name: id
                    type: bigint
                    isNullable: false
                  - name: name
                    type: text
                    isNullable: true
        """;

        File.WriteAllText(Path.Combine(_schemaDirectory, "schema.yaml"), schemaDocument);
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
        // Refresh reads the live database, so it needs no desired-schema options. The state store is defined in
        // nschema.json (state stores are config-only), so we point refresh at a config file declaring the file store.
        await RunCli("apply", [.. SchemaArguments, "--auto-approve"]);
        var stateFile = Path.Combine(_schemaDirectory, "state.json");
        var configFile = WriteConfig(new { state = new { file = new { path = stateFile } } });

        // Act
        var exitCode = await RunCli("refresh", ["--config", configFile]);

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
        await RunCli("apply", [.. SchemaArguments, "--auto-approve"]);
        (await TableExists("widgets")).ShouldBeTrue();

        // Act
        var exitCode = await RunCli("destroy", [.. SchemaArguments, "--auto-approve"]);

        // Assert
        exitCode.ShouldBe(0);
        (await TableExists("widgets")).ShouldBeFalse();
    }

    // The schema directory is the one desired-schema flag (format/pattern are config-only); refresh takes none of them.
    private string[] SchemaArguments => ["--schema-dir", _schemaDirectory];

    private async Task<int> RunCli(string command, string[] commandArguments)
    {
        string[] args = [command, .. commandArguments];

        // The live database is supplied the way real usage does it: via the connection-string environment variable.
        Environment.SetEnvironmentVariable(EnvironmentVariables.PostgresConnectionString, fixture.ConnectionString);
        try
        {
            // Disable the built-in handler so a failure surfaces as a thrown exception in the test rather than exit code 1.
            return await RootCommand.Create()
                .Parse(args)
                .InvokeAsync(new InvocationConfiguration { EnableDefaultExceptionHandler = false });
        }
        finally
        {
            Environment.SetEnvironmentVariable(EnvironmentVariables.PostgresConnectionString, null);
        }
    }

    private string WriteConfig(object config)
    {
        var path = Path.Combine(_schemaDirectory, "nschema.json");
        File.WriteAllText(path, JsonSerializer.Serialize(config));
        return path;
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
