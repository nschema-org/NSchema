using NSchema.Commands.Lock.Release;
using NSchema.Configuration.State;

namespace NSchema.Tests.Commands.Lock.Release;

public sealed class LockReleaseConfigurationValidatorTests
{
    private readonly LockReleaseConfigurationValidator _sut = new();

    private static LockReleaseConfiguration Config(string? lockId = null, bool force = false) => new()
    {
        State = new StateConfig { File = new FileStateConfig { Path = "./state.json" } },
        LockId = lockId,
        Force = force,
    };

    [Fact]
    public void Valid_WithLockId()
    {
        // Arrange — naming the lock is the safe default.
        var config = Config(lockId: "9f8e7d6c");

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Valid_WithForce()
    {
        // Arrange — --force releases whatever lock is held, no id needed.
        var config = Config(force: true);

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Invalid_WhenNeitherLockIdNorForce()
    {
        // Arrange — safe by default: releasing requires naming the lock or opting out with --force.
        var config = Config();

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.ErrorMessage.Contains("needs the id of the lock"));
    }

    [Fact]
    public void Valid_WithBothLockIdAndForce()
    {
        // Arrange — a redundant --force alongside an id is ignored, not an error; the id takes precedence.
        var config = Config(lockId: "9f8e7d6c", force: true);

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Invalid_WhenStateMissing()
    {
        // Arrange — the lock lives with the state store, so there is nothing to release without one. (--force set so
        // only the missing-state rule fires.)
        var config = new LockReleaseConfiguration { State = null, Force = true };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.ErrorMessage.Contains("state store is required"));
    }
}
