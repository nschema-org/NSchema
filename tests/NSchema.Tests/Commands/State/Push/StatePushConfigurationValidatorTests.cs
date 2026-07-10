using NSchema.Commands.State.Push;
using NSchema.Configuration.State;

namespace NSchema.Tests.Commands.State.Push;

public sealed class StatePushConfigurationValidatorTests
{
    private readonly StatePushConfigurationValidator _sut = new();

    [Fact]
    public void Valid_WithStateStore()
    {
        // Arrange
        var config = new StatePushConfiguration
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
        // Arrange — no configured store to push the payload to.
        var config = new StatePushConfiguration { State = null };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.ErrorMessage.Contains("state store is required"));
    }
}
