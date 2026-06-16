using NSchema.Commands.Import;
using NSchema.Configuration.Import;
using NSchema.Configuration.Provider;
using NSchema.Operations.Import;

namespace NSchema.Tests.Commands.Import;

public sealed class ImportConfigurationValidatorTests
{
    private readonly ImportConfigurationValidator _sut = new();

    private static ProviderConfig AProvider() =>
        new() { Postgres = new PostgresProviderConfig { ConnectionString = "Host=localhost" } };

    [Fact]
    public void Valid_WithFileOutput_ForNonePartition()
    {
        // Arrange
        var config = new ImportConfiguration
        {
            Provider = AProvider(),
            Target = new ImportTargetConfig { OutputFile = "./schema.yaml", Partition = ImportPartitionMode.None },
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(ImportPartitionMode.Schema)]
    [InlineData(ImportPartitionMode.Object)]
    public void Valid_WithDirectoryOutput_ForPartitionedModes(ImportPartitionMode partition)
    {
        // Arrange
        var config = new ImportConfiguration
        {
            Provider = AProvider(),
            Target = new ImportTargetConfig { OutputDirectory = "./schemas", Partition = partition },
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Invalid_WhenProviderMissing()
    {
        // Arrange
        var config = new ImportConfiguration
        {
            Provider = new ProviderConfig(),
            Target = new ImportTargetConfig { OutputFile = "./schema.yaml", Partition = ImportPartitionMode.None },
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.ErrorMessage.Contains("database provider is required"));
    }

    [Fact]
    public void Invalid_WhenNonePartition_HasNoOutputFile()
    {
        // Arrange
        var config = new ImportConfiguration
        {
            Provider = AProvider(),
            Target = new ImportTargetConfig { Partition = ImportPartitionMode.None },
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.ErrorMessage.Contains("output file is required"));
    }

    [Fact]
    public void Invalid_WhenNonePartition_HasOutputDirectory()
    {
        // Arrange
        var config = new ImportConfiguration
        {
            Provider = AProvider(),
            Target = new ImportTargetConfig { OutputFile = "./schema.yaml", OutputDirectory = "./schemas", Partition = ImportPartitionMode.None },
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.ErrorMessage.Contains("Output directory is not used"));
    }

    [Fact]
    public void Valid_WhenPartitionedMode_HasNoOutputDirectory()
    {
        // Arrange
        var config = new ImportConfiguration
        {
            Provider = AProvider(),
            Target = new ImportTargetConfig { Partition = ImportPartitionMode.Schema },
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Invalid_WhenPartitionedMode_HasOutputFile()
    {
        // Arrange
        var config = new ImportConfiguration
        {
            Provider = AProvider(),
            Target = new ImportTargetConfig
            {
                OutputDirectory = "./schemas",
                OutputFile = "./schema.yaml",
                Partition = ImportPartitionMode.Object
            },
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.ErrorMessage.Contains("Output file is not used"));
    }
}
