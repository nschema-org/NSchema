using NSchema.Commands.Destroy;
using NSchema.Configuration.Provider;
using NSchema.Configuration.Schema;
using NSchema.Configuration.State;

namespace NSchema.Tests.Commands.Destroy;

public sealed class DestroyConfigurationValidatorTests
{
    private readonly DestroyConfigurationValidator _sut = new();

    [Fact]
    public void Valid_WithProviderAndState()
    {
        // Arrange
        var config = new DestroyConfiguration
        {
            Provider = new ProviderConfig { Postgres = new PostgresProviderConfig { ConnectionString = "Host=localhost" } },
            State = new StateConfig { File = new FileStateConfig { Path = "./state.json" } },
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Valid_WithProviderAndSchema_NoStore()
    {
        // Arrange
        var config = new DestroyConfiguration
        {
            Provider = new ProviderConfig { Postgres = new PostgresProviderConfig { ConnectionString = "Host=localhost" } },
            Schema = new SchemaConfig { Directory = "./schema" },
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
        var config = new DestroyConfiguration
        {
            Provider = new ProviderConfig(),
            State = new StateConfig { File = new FileStateConfig { Path = "./state.json" } },
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.ErrorMessage.Contains("database provider is required"));
    }

    [Fact]
    public void Invalid_WhenNoManagedSchemaSource()
    {
        // Arrange — provider present, but neither a state store nor a desired schema to tear down.
        var config = new DestroyConfiguration
        {
            Provider = new ProviderConfig { Postgres = new PostgresProviderConfig { ConnectionString = "Host=localhost" } },
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.ErrorMessage.Contains("managed schema source"));
    }
}
