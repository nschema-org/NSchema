using NSchema.Cli.Configuration.Provider;

namespace NSchema.Cli.Tests.Configuration.Provider;

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
}
