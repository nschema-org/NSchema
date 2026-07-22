using NSchema.Commands.Refresh;
using NSchema.Configuration.State;

namespace NSchema.Tests.Commands.Refresh;

public sealed class RefreshConfigurationValidatorTests
{
    private readonly RefreshConfigurationValidator _sut = new();

    [Fact]
    public void Valid_WithProviderAndState()
    {
        // Arrange
        var config = new RefreshConfiguration
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
    public void Invalid_WhenProviderMissing()
    {
        // Arrange
        var config = new RefreshConfiguration
        {
            Database = null,
            State = new StateConfiguration { File = new FileStateConfiguration { Path = "./state.json" } },
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.ErrorMessage.Contains("database provider is required"));
    }

    [Fact]
    public void Invalid_WhenStateMissing()
    {
        // Arrange
        var config = new RefreshConfiguration
        {
            Database = TestConfigurations.Provider(),
            State = null,
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.ErrorMessage.Contains("state store is required"));
    }
}
