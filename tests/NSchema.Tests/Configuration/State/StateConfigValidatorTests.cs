using NSchema.Cli.Configuration.State;

namespace NSchema.Cli.Tests.Configuration.State;

public sealed class StateConfigValidatorTests
{
    private readonly StateConfigValidator _sut = new();

    [Fact]
    public void Valid_WhenNoStoreConfigured()
    {
        // Arrange
        var config = new StateConfig();

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Valid_ForFileWithPath()
    {
        // Arrange
        var config = new StateConfig { File = new FileStateConfig { Path = "./state.json" } };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Valid_ForS3WithBucketAndKey()
    {
        // Arrange
        var config = new StateConfig { S3 = new S3StateConfig { Bucket = "bucket", Key = "key" } };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Invalid_WhenS3KeyMissing()
    {
        // Arrange
        var config = new StateConfig { S3 = new S3StateConfig { Bucket = "bucket" } };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.ErrorMessage.Contains("state.s3.key"));
    }

    [Fact]
    public void Invalid_WhenMoreThanOneStoreConfigured()
    {
        // Arrange
        var config = new StateConfig
        {
            File = new FileStateConfig { Path = "./state.json" },
            S3 = new S3StateConfig { Bucket = "bucket", Key = "key" },
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.ErrorMessage.Contains("exactly one"));
    }
}
