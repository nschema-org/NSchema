using NSchema.Cli.Configuration;

namespace NSchema.Cli.Tests.Configuration;

public sealed class SchemaFormatExtensionsTests
{
    [Fact]
    public void DefaultGlob_ForYaml_ReturnsYamlPattern()
    {
        // Act
        var glob = SchemaFormat.Yaml.DefaultGlob();

        // Assert
        glob.ShouldBe("**/*.yaml");
    }

    [Fact]
    public void DefaultGlob_ForJson_ReturnsJsonPattern()
    {
        // Act
        var glob = SchemaFormat.Json.DefaultGlob();

        // Assert
        glob.ShouldBe("**/*.json");
    }
}
