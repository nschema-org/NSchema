using NSchema.Cli.Commands.Import;
using NSchema.Cli.Configuration.Import;
using NSchema.Cli.Configuration.Provider;
using NSchema.Import;

namespace NSchema.Cli.Tests.Commands.Import;

public sealed class ImportConfigurationValidatorTests
{
    private readonly ImportConfigurationValidator _sut = new();

    [Fact]
    public void Valid_WithProviderAndOutputPath()
    {
        // Arrange
        var config = new ImportConfiguration
        {
            Provider = new ProviderConfig { Postgres = new PostgresProviderConfig { ConnectionString = "Host=localhost" } },
            ImportTarget = new ImportTargetConfig { OutputPath = "./schemas" },
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
            ImportTarget = new ImportTargetConfig { OutputPath = "./schemas" },
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.ErrorMessage.Contains("database provider is required"));
    }

    [Fact]
    public void Invalid_WhenOutputPathMissing()
    {
        // Arrange
        var config = new ImportConfiguration
        {
            Provider = new ProviderConfig { Postgres = new PostgresProviderConfig { ConnectionString = "Host=localhost" } },
            ImportTarget = new ImportTargetConfig { OutputPath = "" },
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.ErrorMessage.Contains("output path is required"));
    }

    [Theory]
    [InlineData(ImportPartitionMode.None)]
    [InlineData(ImportPartitionMode.Schema)]
    [InlineData(ImportPartitionMode.Table)]
    public void Valid_WithAllPartitionModes(ImportPartitionMode partition)
    {
        // Arrange
        var config = new ImportConfiguration
        {
            Provider = new ProviderConfig { Postgres = new PostgresProviderConfig { ConnectionString = "Host=localhost" } },
            ImportTarget = new ImportTargetConfig { OutputPath = "./schemas", Partition = partition },
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeTrue();
    }
}
