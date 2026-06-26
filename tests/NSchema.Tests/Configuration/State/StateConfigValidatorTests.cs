using NSchema.Configuration.State;

namespace NSchema.Tests.Configuration.State;

public sealed class StateConfigValidatorTests
{
    private readonly StateConfigValidator _sut = new();

    [Fact]
    public void Valid_WhenNoStoreConfigured()
        => _sut.Validate(new StateConfig()).IsValid.ShouldBeTrue();

    [Fact]
    public void Valid_ForFileWithPath()
        => _sut.Validate(new StateConfig { File = new FileStateConfig { Path = "./state.json" } }).IsValid.ShouldBeTrue();

    [Fact]
    public void Valid_ForPluginBackend()
        // The slice validator only checks presence; backend-specific attribute validation lives in the plugin.
        => _sut.Validate(TestConfigs.S3State()).IsValid.ShouldBeTrue();

    [Fact]
    public void Invalid_WhenMoreThanOneStoreConfigured()
    {
        // Arrange
        var config = new StateConfig
        {
            File = new FileStateConfig { Path = "./state.json" },
            Plugin = TestConfigs.S3State().Plugin,
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.ErrorMessage.Contains("exactly one"));
    }
}
