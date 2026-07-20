using NSchema.Commands.Destroy;
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
    public void Invalid_WhenStateMissing()
    {
        // Arrange — the managed schema is read from the recorded state, so a store is required.
        var config = new DestroyConfiguration
        {
            Provider = TestConfigs.Provider(),
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.ErrorMessage.Contains("state store is required"));
    }

    [Fact]
    public void Valid_WithEphemeral_InsteadOfAStore()
    {
        // Arrange — --ephemeral stands in for a configured store (CI against a disposable database).
        var config = new DestroyConfiguration
        {
            Provider = TestConfigs.Provider(),
            Ephemeral = true,
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
            Provider = null,
            State = new StateConfig { File = new FileStateConfig { Path = "./state.json" } },
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.ErrorMessage.Contains("database provider is required"));
    }
}
