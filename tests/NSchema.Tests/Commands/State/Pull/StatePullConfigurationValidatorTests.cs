using NSchema.Commands.State.Pull;
using NSchema.Configuration.State;

namespace NSchema.Tests.Commands.State.Pull;

public sealed class StatePullConfigurationValidatorTests
{
    private readonly StatePullConfigurationValidator _sut = new();

    [Fact]
    public void Valid_WithStateStore()
    {
        // Arrange
        var config = new StatePullConfiguration
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
        // Arrange — no configured store to pull the recorded state from.
        var config = new StatePullConfiguration { State = null };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.ErrorMessage.Contains("state store is required"));
    }
}
