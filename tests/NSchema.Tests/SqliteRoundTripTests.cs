using Microsoft.Data.Sqlite;
using NSchema.Configuration;
using NSchema.Tests.Fixtures;

namespace NSchema.Tests;

/// <summary>
/// Runs the shared <see cref="ProviderRoundTripTests"/> scenario against a live SQLite file database. The database
/// itself is in-process (no container), and every object lives under the single <c>main</c> schema, so the desired
/// schema declares its table there (no CREATE SCHEMA). The s3-backend state tests still use the shared MinIO container.
/// </summary>
public sealed class SqliteRoundTripTests(MinioFixture minio) : ProviderRoundTripTests
{
    // A dedicated query connection string (pooling off) so it never holds the file open against the temp-dir cleanup.
    private string QueryConnectionString => $"Data Source={DatabasePath};Pooling=False";

    private string DatabasePath => Path.Combine(ProjectDirectory, "app.db");

    protected override MinioFixture Minio => minio;

    protected override string ConnectionEnvironmentVariable => EnvironmentVariables.SqliteConnectionString;

    protected override string ConnectionString => $"Data Source={DatabasePath}";

    protected override string InitialSchemaDdl =>
        """
        CREATE TABLE main.widgets (
          id   bigint NOT NULL,
          name text,
          CONSTRAINT widgets_pkey PRIMARY KEY (id)
        );
        """;

    protected override string EvolvedSchemaDdl =>
        """
        CREATE TABLE main.widgets (
          id       bigint NOT NULL,
          name     text,
          quantity bigint,
          CONSTRAINT widgets_pkey PRIMARY KEY (id)
        );
        """;

    protected override string DropNameColumnSql => "ALTER TABLE main.widgets DROP COLUMN name";

    protected override async Task<bool> TableExists(string table)
    {
        await using var connection = new SqliteConnection(QueryConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name = $table";
        command.Parameters.AddWithValue("$table", table);
        return Convert.ToInt64(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken)) > 0;
    }

    protected override async Task ExecuteSql(string sql)
    {
        await using var connection = new SqliteConnection(QueryConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }
}
