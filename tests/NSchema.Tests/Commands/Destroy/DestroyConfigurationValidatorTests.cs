using NSchema.Commands.Destroy;
using NSchema.Configuration.Provider;
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
            Provider = TestConfigs.Provider(),
            State = new StateConfig { File = new FileStateConfig { Path = "./state.json" } },
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Valid_WithProviderOnly_FallsBackToWorkingDirectorySchema()
    {
        // Arrange — with no state store, the managed schema is the *.sql files under the working directory, so a
        // provider alone is sufficient.
        var config = new DestroyConfiguration
        {
            Provider = TestConfigs.Provider(),
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
}
