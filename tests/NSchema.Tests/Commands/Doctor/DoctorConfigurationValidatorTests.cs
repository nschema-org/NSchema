using NSchema.Commands.Doctor;
using NSchema.Configuration.State;

namespace NSchema.Tests.Commands.Doctor;

public sealed class DoctorConfigurationValidatorTests
{
    private readonly DoctorConfigurationValidator _sut = new();

    [Fact]
    public void Valid_WithProviderAndState()
    {
        // Arrange
        var config = new DoctorConfiguration
        {
            Database = TestConfigurations.Provider(),
            State = new StateConfiguration { File = new FileStateConfiguration { Path = "./state.json" } },
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Valid_WithProviderOnly()
    {
        // Arrange — doctor can check just the database when no state store is declared.
        var config = new DoctorConfiguration
        {
            Database = TestConfigurations.Provider(),
            State = null,
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Valid_WithStateOnly()
    {
        // Arrange — doctor can check just the state store when no provider is declared (offline project).
        var config = new DoctorConfiguration
        {
            Database = null,
            State = new StateConfiguration { File = new FileStateConfiguration { Path = "./state.json" } },
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Invalid_WhenNothingConfigured()
    {
        // Arrange — neither a provider nor a state store means there is nothing for doctor to probe.
        var config = new DoctorConfiguration
        {
            Database = null,
            State = null,
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.ErrorMessage.Contains("Nothing to check"));
    }
}
