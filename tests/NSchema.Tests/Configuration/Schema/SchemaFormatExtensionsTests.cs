using NSchema.Configuration.Schema;

namespace NSchema.Tests.Configuration.Schema;

public sealed class SchemaFormatExtensionsTests
{
    [Fact]
    public void DefaultPattern_ForYaml_ReturnsYamlPattern()
    {
        // Act
        var glob = SchemaFormat.Yaml.DefaultPattern();

        // Assert
        glob.ShouldBe("**/*.yaml");
    }

    [Fact]
    public void DefaultPattern_ForJson_ReturnsJsonPattern()
    {
        // Act
        var glob = SchemaFormat.Json.DefaultPattern();

        // Assert
        glob.ShouldBe("**/*.json");
    }
}
