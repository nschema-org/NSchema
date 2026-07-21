using NSchema.Commands.Lock.Acquire;
using NSchema.Configuration.State;

namespace NSchema.Tests.Commands.Lock.Acquire;

public sealed class LockAcquireConfigurationValidatorTests
{
    private readonly LockAcquireConfigurationValidator _sut = new();

    [Fact]
    public void Valid_WithStateStore()
    {
        // Arrange — default reason ("manual"), no TTL.
        var config = new LockAcquireConfiguration
        {
            State = new StateConfiguration { File = new FileStateConfiguration { Path = "./state.json" } },
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Invalid_WhenStateMissing()
    {
        // Arrange — the lock lives with the state store, so there is nothing to acquire without one.
        var config = new LockAcquireConfiguration { State = null };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.ErrorMessage.Contains("state store is required"));
    }
}
