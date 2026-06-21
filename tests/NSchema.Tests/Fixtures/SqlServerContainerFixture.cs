using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace NSchema.Tests.Fixtures;

/// <summary>
/// Starts a throwaway SQL Server container (<c>mcr.microsoft.com/mssql/server</c>) shared across the integration test
/// collection. Docker must be running locally.
/// </summary>
public sealed class SqlServerContainerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    public string ConnectionString { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();
    }

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();

    /// <summary>Opens a fresh connection against the container so tests can query and seed the live database.</summary>
    public SqlConnection OpenConnection()
    {
        var connection = new SqlConnection(ConnectionString);
        connection.Open();
        return connection;
    }
}
