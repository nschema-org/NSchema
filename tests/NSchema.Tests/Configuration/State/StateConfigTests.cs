using NSchema.Configuration.State;

namespace NSchema.Tests.Configuration.State;

public sealed class StateConfigTests
{
    private readonly StateConfig _sut = new();

    [Fact]
    public void SetFilePath_CreatesFileSectionOnDemand()
    {
        // Act
        _sut.SetFilePath("./state.json");

        // Assert
        _sut.File.ShouldNotBeNull();
        _sut.File.Path.ShouldBe("./state.json");
        _sut.S3.ShouldBeNull();
    }

    [Fact]
    public void SetS3Bucket_And_SetS3Key_ShareOneSection()
    {
        // Act
        _sut.SetS3Bucket("bucket");
        _sut.SetS3Key("key");

        // Assert
        _sut.S3.ShouldNotBeNull();
        _sut.S3.Bucket.ShouldBe("bucket");
        _sut.S3.Key.ShouldBe("key");
        _sut.ConfiguredSectionCount.ShouldBe(1);
    }
}
