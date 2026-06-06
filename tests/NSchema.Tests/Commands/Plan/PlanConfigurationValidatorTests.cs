using NSchema.Commands.Plan;
using NSchema.Configuration.Provider;
using NSchema.Configuration.Schema;
using NSchema.Configuration.State;

namespace NSchema.Tests.Commands.Plan;

public sealed class PlanConfigurationValidatorTests
{
    private readonly PlanConfigurationValidator _sut = new();

    [Fact]
    public void Valid_WithProviderOnly()
    {
        // Arrange
        var config = new PlanConfiguration
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
    public void Valid_WithStateOnly_ForOfflinePlanning()
    {
        // Arrange
        var config = new PlanConfiguration
        {
            Schema = new SchemaConfig { Directory = "./schemas" },
            Provider = new ProviderConfig(),
            State = new StateConfig { File = new FileStateConfig { Path = "./state.json" } },
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Invalid_WhenNeitherProviderNorStateConfigured()
    {
        // Arrange
        var config = new PlanConfiguration
        {
            Schema = new SchemaConfig { Directory = "./schemas" },
            Provider = new ProviderConfig(),
            State = new StateConfig(),
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.ErrorMessage.Contains("current schema source"));
    }
}
