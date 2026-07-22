using NSchema.Configuration.State;

namespace NSchema.Tests.Configuration.State;

public sealed class StateConfigurationValidatorTests
{
    private readonly StateConfigurationValidator _sut = new();

    [Fact]
    public void Valid_WhenNoStoreConfigured()
        => _sut.Validate(new StateConfiguration()).IsValid.ShouldBeTrue();

    [Fact]
    public void Valid_ForFileWithPath()
        => _sut.Validate(new StateConfiguration { File = new FileStateConfiguration { Path = "./state.json" } }).IsValid.ShouldBeTrue();

    [Fact]
    public void Valid_ForPluginBackend()
        // The slice validator only checks presence; backend-specific attribute validation lives in the plugin.
        => _sut.Validate(TestConfigurations.S3State()).IsValid.ShouldBeTrue();

    [Fact]
    public void Invalid_WhenMoreThanOneStoreConfigured()
    {
        // Arrange
        var config = new StateConfiguration
        {
            File = new FileStateConfiguration { Path = "./state.json" },
            Plugin = TestConfigurations.S3State().Plugin,
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.ErrorMessage.Contains("exactly one"));
    }
}
