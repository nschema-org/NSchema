using NSchema.Configuration.Provider;

namespace NSchema.Tests.Configuration.Provider;

public sealed class ProviderConfigValidatorTests
{
    private readonly ProviderConfigValidator _sut = new();

    [Fact]
    public void Valid_WhenNoProviderConfigured()
    {
        // Arrange
        var config = new ProviderConfig();

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Valid_ForPostgresWithConnectionString()
    {
        // Arrange
        var config = new ProviderConfig { Postgres = new PostgresProviderConfig { ConnectionString = "Host=localhost" } };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Invalid_WhenConnectionStringMissing()
    {
        // Arrange
        var config = new ProviderConfig { Postgres = new PostgresProviderConfig() };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.ErrorMessage.Contains("connectionString"));
    }

    [Fact]
    public void Invalid_WhenCommandTimeoutNegative()
    {
        // Arrange
        var config = new ProviderConfig
        {
            Postgres = new PostgresProviderConfig { ConnectionString = "Host=localhost", CommandTimeout = -1 },
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.ErrorMessage.Contains("commandTimeout"));
    }

    [Fact]
    public void Valid_ForSqliteWithConnectionString()
    {
        // Arrange
        var config = new ProviderConfig { Sqlite = new SqliteProviderConfig { ConnectionString = "Data Source=app.db" } };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Invalid_WhenSqliteConnectionStringMissing()
    {
        // Arrange
        var config = new ProviderConfig { Sqlite = new SqliteProviderConfig() };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.ErrorMessage.Contains("connectionString"));
    }

    [Fact]
    public void Valid_ForSqlServerWithConnectionString()
    {
        // Arrange
        var config = new ProviderConfig { SqlServer = new SqlServerProviderConfig { ConnectionString = "Server=localhost" } };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Invalid_WhenSqlServerConnectionStringMissing()
    {
        // Arrange
        var config = new ProviderConfig { SqlServer = new SqlServerProviderConfig() };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.ErrorMessage.Contains("connectionString"));
    }

    [Fact]
    public void Invalid_WhenSqlServerCommandTimeoutNegative()
    {
        // Arrange
        var config = new ProviderConfig
        {
            SqlServer = new SqlServerProviderConfig { ConnectionString = "Server=localhost", CommandTimeout = -1 },
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.ErrorMessage.Contains("commandTimeout"));
    }

    [Fact]
    public void Invalid_WhenMoreThanOneProviderConfigured()
    {
        // Arrange — a project may declare exactly one provider; Postgres and SQLite together is a misconfiguration.
        var config = new ProviderConfig
        {
            Postgres = new PostgresProviderConfig { ConnectionString = "Host=localhost" },
            Sqlite = new SqliteProviderConfig { ConnectionString = "Data Source=app.db" },
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.ErrorMessage.Contains("More than one database provider"));
    }
}
