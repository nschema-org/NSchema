using NSchema.Commands.Database.Show;
using NSchema.Configuration.Plugins;

namespace NSchema.Tests.Commands.Database.Show;

public sealed class DatabaseShowConfigurationValidatorTests
{
    private readonly DatabaseShowConfigurationValidator _sut = new();

    private static PluginReference Postgres() => TestConfigurations.Provider();

    [Fact]
    public void Valid_WithProvider()
    {
        // Arrange — db show reads the live schema, so a provider is all it needs.
        var config = new DatabaseShowConfiguration { Provider = Postgres() };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Invalid_WhenProviderMissing()
    {
        // Arrange — without a provider there is no live database to read.
        var config = new DatabaseShowConfiguration { Provider = null };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.ErrorMessage.Contains("database provider is required"));
    }
}
