using NSchema.Commands.Validate;
using NSchema.Configuration.Schema;

namespace NSchema.Tests.Commands.Validate;

public sealed class ValidateConfigurationValidatorTests
{
    private readonly ValidateConfigurationValidator _sut = new();

    [Fact]
    public void Valid_WithSchemaDirectoryOnly()
    {
        // Arrange
        var config = new ValidateConfiguration
        {
            Schema = new SchemaConfig { Directory = "./schemas" },
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Invalid_WhenSchemaDirectoryMissing()
    {
        // Arrange
        var config = new ValidateConfiguration
        {
            Schema = new SchemaConfig { Directory = "" },
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.ErrorMessage.Contains("schema directory"));
    }

    [Fact]
    public void Invalid_WhenFormatNotInEnum()
    {
        // Arrange
        var config = new ValidateConfiguration
        {
            Schema = new SchemaConfig { Directory = "./schemas", Format = (SchemaFormat)42 },
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeFalse();
    }
}
