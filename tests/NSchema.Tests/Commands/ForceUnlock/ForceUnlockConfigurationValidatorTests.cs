using NSchema.Commands.ForceUnlock;
using NSchema.Configuration.State;

namespace NSchema.Tests.Commands.ForceUnlock;

public sealed class ForceUnlockConfigurationValidatorTests
{
    private readonly ForceUnlockConfigurationValidator _sut = new();

    [Fact]
    public void Valid_WithStateStore()
    {
        // Arrange
        var config = new ForceUnlockConfiguration
        {
            State = new StateConfig { File = new FileStateConfig { Path = "./state.json" } },
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Invalid_WhenStateMissing()
    {
        // Arrange
        var config = new ForceUnlockConfiguration
        {
            State = new StateConfig(),
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.ErrorMessage.Contains("state store is required"));
    }
}
