using NSchema.Commands.Lock.Status;
using NSchema.Configuration.State;

namespace NSchema.Tests.Commands.Lock.Status;

public sealed class LockStatusConfigurationValidatorTests
{
    private readonly LockStatusConfigurationValidator _sut = new();

    [Fact]
    public void Valid_WithStateStore()
    {
        // Arrange
        var config = new LockStatusConfiguration
        {
            State = new StateConfiguration { File = new FileStateConfiguration { Path = "./state.json" } },
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Invalid_WhenStateStoreMissing()
    {
        // Arrange — the lock lives with the state store, so there is nothing to inspect without one.
        var config = new LockStatusConfiguration { State = null };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.ErrorMessage.Contains("state store is required"));
    }
}
