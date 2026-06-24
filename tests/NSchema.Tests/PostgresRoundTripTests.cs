using NSchema.Commands;
using NSchema.Configuration;
using NSchema.Tests.Fixtures;

namespace NSchema.Tests;

/// <summary>
/// Runs the shared <see cref="ProviderRoundTripTests"/> scenario against a live PostgreSQL container, plus a
/// Postgres-specific constraint-comment round-trip. Each test uses a unique database schema for isolation.
/// </summary>
[Collection("postgres")]
public sealed class PostgresRoundTripTests(PostgresContainerFixture fixture, MinioFixture minio) : ProviderRoundTripTests
{
    private readonly string _schema = $"test_{Guid.NewGuid():N}";

    protected override MinioFixture Minio => minio;

    protected override string ConnectionEnvironmentVariable => EnvironmentVariables.PostgresConnectionString;

    protected override string ConnectionString => fixture.ConnectionString;

    protected override string InitialSchemaDdl =>
        $"""
        CREATE SCHEMA {_schema};

        CREATE TABLE {_schema}.widgets (
          id   bigint NOT NULL,
          name text,
          CONSTRAINT widgets_pkey PRIMARY KEY (id)
        );
        """;

    protected override string EvolvedSchemaDdl =>
        $"""
        CREATE SCHEMA {_schema};

        CREATE TABLE {_schema}.widgets (
          id       bigint  NOT NULL,
          name     text,
          quantity integer,
          CONSTRAINT widgets_pkey PRIMARY KEY (id)
        );
        """;

    protected override string DropNameColumnSql => $"ALTER TABLE \"{_schema}\".widgets DROP COLUMN name";

    protected override async Task<bool> TableExists(string table)
    {
        await using var command = fixture.DataSource.CreateCommand(
            "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = @schema AND table_name = @table)");
        command.Parameters.AddWithValue("schema", _schema);
        command.Parameters.AddWithValue("table", table);
        return (bool)(await command.ExecuteScalarAsync())!;
    }

    protected override async Task ExecuteSql(string sql)
    {
        await using var command = fixture.DataSource.CreateCommand(sql);
        await command.ExecuteNonQueryAsync();
    }

    protected override async Task CleanupDatabase()
    {
        await using var command = fixture.DataSource.CreateCommand($"DROP SCHEMA IF EXISTS \"{_schema}\" CASCADE");
        await command.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task Apply_ThenPlan_WithUniqueAndCheckConstraints_RoundTripsCleanly()
    {
        // Arrange — a schema exercising unique and check constraints (with constraint comments), which the Postgres
        // provider both generates (as separate ALTER TABLE ADD CONSTRAINT statements) and introspects. Applying then
        // re-planning must round-trip cleanly. The `integer` spelling exercises the DDL's alias to the canonical
        // `int`, which introspection reports — without it, the re-plan would drift on the column type.
        await WriteSchema(
            $"""
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
            """);

        // Act
        var (applyExit, _) = await RunCli("apply", "--auto-approve");
        var (planExit, planOutput) = await RunCli("plan");

        // Assert — the apply succeeds, both constraints land with their comments, and the re-plan finds nothing to do.
        applyExit.ShouldBe(ExitCodes.NoChanges);
        (await ConstraintComment("accounts_email_key")).ShouldBe("one account per email address");
        (await ConstraintComment("accounts_balance_check")).ShouldBe("balances may never go negative");
        planExit.ShouldBe(ExitCodes.NoChanges);
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
}
