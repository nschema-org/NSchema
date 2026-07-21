using NSchema.Commands.Import;
using NSchema.Configuration.Plugins;

namespace NSchema.Tests.Commands.Import;

public sealed class ImportConfigurationValidatorTests
{
    private readonly ImportConfigurationValidator _sut = new();

    private static PluginReference AProvider() => TestConfigurations.Provider();

    [Fact]
    public void Valid_WithProvider()
    {
        // Arrange
        var config = new ImportConfiguration { Database = AProvider(), OutputDirectory = "./schemas" };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Valid_WithoutOutputDirectory()
    {
        // Arrange — the output directory is optional; import defaults to the current directory.
        var config = new ImportConfiguration { Database = AProvider() };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Invalid_WhenProviderMissing()
    {
        // Arrange
        var config = new ImportConfiguration { Database = null, OutputDirectory = "./schemas" };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.ErrorMessage.Contains("database provider is required"));
    }
}
