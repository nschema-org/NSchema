using NSchema.Commands.Apply;
using NSchema.Configuration.State;

namespace NSchema.Tests.Commands.Apply;

public sealed class ApplyConfigurationValidatorTests
{
    private readonly ApplyConfigurationValidator _sut = new();

    [Fact]
    public void Valid_WithProvider()
    {
        // Arrange
        var config = new ApplyConfiguration
        {
            Provider = TestConfigs.Provider(),
            State = null,
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
