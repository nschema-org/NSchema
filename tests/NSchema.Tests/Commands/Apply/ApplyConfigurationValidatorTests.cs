using NSchema.Commands.Apply;
using NSchema.Configuration.State;

namespace NSchema.Tests.Commands.Apply;

public sealed class ApplyConfigurationValidatorTests
{
    private readonly ApplyConfigurationValidator _sut = new();

    [Fact]
    public void Valid_WithProviderAndState()
    {
        // Arrange — apply executes against the database and records what it ran in the state store.
        var config = new ApplyConfiguration
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
        // Arrange — an apply that cannot record what it ran would silently lose history.
        var config = new ApplyConfiguration
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

    [Fact]
    public void Valid_WithEphemeralState_InsteadOfAStore()
    {
        // Arrange — --ephemeral-state stands in for a configured store (CI against a disposable database).
        var config = new ApplyConfiguration
        {
            Provider = TestConfigs.Provider(),
            State = null,
            EphemeralState = true,
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
        var config = new ApplyConfiguration
        {
            Provider = null,
            State = null,
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.ErrorMessage.Contains("database provider is required"));
    }
}
