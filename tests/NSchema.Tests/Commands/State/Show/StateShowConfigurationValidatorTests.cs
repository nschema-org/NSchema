using NSchema.Commands.State.Show;
using NSchema.Configuration.State;

namespace NSchema.Tests.Commands.State.Show;

public sealed class StateShowConfigurationValidatorTests
{
    private readonly StateShowConfigurationValidator _sut = new();

    [Fact]
    public void Valid_WithStateStore()
    {
        // Arrange — without a file, the recorded state comes from the configured store.
        var config = new StateShowConfiguration
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
        // Arrange — no configured store to read the recorded state from.
        var config = new StateShowConfiguration { State = null };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.ErrorMessage.Contains("state store is required"));
    }
}
