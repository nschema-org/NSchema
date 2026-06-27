using NSchema.Commands.Drift;
using NSchema.Configuration.State;

namespace NSchema.Tests.Commands.Drift;

public sealed class DriftConfigurationValidatorTests
{
    private readonly DriftConfigurationValidator _sut = new();

    [Fact]
    public void Valid_WithProviderAndState()
    {
        // Arrange
        var config = new DriftConfiguration
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
    public void Invalid_WhenProviderMissing()
    {
        // Arrange
        var config = new DriftConfiguration
        {
            Provider = null,
            State = new StateConfig { File = new FileStateConfig { Path = "./state.json" } },
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.ErrorMessage.Contains("database provider is required"));
    }

    [Fact]
    public void Invalid_WhenStateMissing()
    {
        // Arrange
        var config = new DriftConfiguration
        {
            Provider = TestConfigs.Provider(),
            State = null,
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.ErrorMessage.Contains("state store is required"));
    }
}
