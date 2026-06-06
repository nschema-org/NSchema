using NSchema.Commands.Apply;
using NSchema.Configuration.Provider;
using NSchema.Configuration.Schema;
using NSchema.Configuration.State;

namespace NSchema.Tests.Commands.Apply;

public sealed class ApplyConfigurationValidatorTests
{
    private readonly ApplyConfigurationValidator _sut = new();

    [Fact]
    public void Valid_WithSchemaAndProvider()
    {
        // Arrange
        var config = new ApplyConfiguration
        {
            Schema = new SchemaConfig { Directory = "./schemas" },
            Provider = new ProviderConfig { Postgres = new PostgresProviderConfig { ConnectionString = "Host=localhost" } },
            State = new StateConfig(),
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Invalid_WhenProviderMissing()
    {
        // Arrange
        var config = new ApplyConfiguration
        {
            Schema = new SchemaConfig { Directory = "./schemas" },
            Provider = new ProviderConfig(),
            State = new StateConfig(),
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.ErrorMessage.Contains("database provider is required"));
    }

    [Fact]
    public void Invalid_WhenSchemaDirectoryMissing()
    {
        // Arrange
        var config = new ApplyConfiguration
        {
            Schema = new SchemaConfig(),
            Provider = new ProviderConfig { Postgres = new PostgresProviderConfig { ConnectionString = "Host=localhost" } },
            State = new StateConfig(),
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.ErrorMessage.Contains("schema directory"));
    }
}
