using NSchema.Configuration.Schema;

namespace NSchema.Tests.Configuration.Schema;

public sealed class SchemaConfigValidatorTests
{
    private readonly SchemaConfigValidator _sut = new();

    [Fact]
    public void Valid_ForDirectory()
    {
        // Arrange
        var config = new SchemaConfig { Directory = "./schema" };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Invalid_WhenDirectoryMissing()
    {
        // Arrange
        var config = new SchemaConfig();

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.ErrorMessage.Contains("schema directory"));
    }
}
