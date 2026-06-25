using NSchema.Configuration;
using NSchema.Tests.Fixtures;

namespace NSchema.Tests;

/// <summary>
/// Runs the shared <see cref="ProviderRoundTripTests"/> scenario against a live SQL Server container. Each test uses a
/// unique database schema for isolation.
/// </summary>
[Collection("sqlserver")]
public sealed class SqlServerRoundTripTests(SqlServerContainerFixture fixture, MinioFixture minio) : ProviderRoundTripTests
{
    private readonly string _schema = $"test_{Guid.NewGuid():N}";

    protected override MinioFixture Minio => minio;

    protected override string ConnectionEnvironmentVariable => EnvironmentVariables.SqlServerConnectionString;

    protected override string ConnectionString => fixture.ConnectionString;

    protected override string InitialSchemaDdl =>
        $"""
        CREATE SCHEMA {_schema};

        CREATE TABLE {_schema}.widgets (
          id   int NOT NULL,
          name varchar(100),
          CONSTRAINT widgets_pkey PRIMARY KEY (id)
        );
        """;

    protected override string EvolvedSchemaDdl =>
        $"""
        CREATE SCHEMA {_schema};

        CREATE TABLE {_schema}.widgets (
          id       int NOT NULL,
          name     varchar(100),
          quantity int,
          CONSTRAINT widgets_pkey PRIMARY KEY (id)
        );
        """;

    protected override string DropNameColumnSql => $"ALTER TABLE [{_schema}].[widgets] DROP COLUMN name";

    protected override async Task<bool> TableExists(string table)
    {
        await using var connection = fixture.OpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT CASE WHEN EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = @schema AND table_name = @table) THEN 1 ELSE 0 END";
        command.Parameters.AddWithValue("@schema", _schema);
        command.Parameters.AddWithValue("@table", table);
        return (int)(await command.ExecuteScalarAsync())! == 1;
    }

    protected override async Task ExecuteSql(string sql)
    {
        await using var connection = fixture.OpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    protected override async Task CleanupDatabase()
    {
        await using var connection = fixture.OpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE IF EXISTS [{_schema}].[widgets]; DROP SCHEMA IF EXISTS [{_schema}];";
        await command.ExecuteNonQueryAsync();
    }
}
