using NSchema.Commands.Db.Show;
using NSchema.Configuration;
using NSchema.Configuration.Plugins;

namespace NSchema.Tests.Commands.Db.Show;

public sealed class DbShowConfigurationValidatorTests
{
    private readonly DbShowConfigurationValidator _sut = new();

    private static PluginReference Postgres() =>
        new("NSchema.Postgres", "4.0.0", "postgres", new ConfigBlock("provider", "postgres", new Dictionary<string, ConfigValue>()));

    [Fact]
    public void Valid_WithProvider()
    {
        // Arrange — db show reads the live schema, so a provider is all it needs.
        var config = new DbShowConfiguration { Provider = Postgres() };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Invalid_WhenProviderMissing()
    {
        // Arrange — without a provider there is no live database to read.
        var config = new DbShowConfiguration { Provider = null };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.ErrorMessage.Contains("database provider is required"));
    }
}
