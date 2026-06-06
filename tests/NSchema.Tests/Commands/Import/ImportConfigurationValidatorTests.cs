using NSchema.Cli.Commands.Import;
using NSchema.Cli.Configuration.Import;
using NSchema.Cli.Configuration.Provider;
using NSchema.Cli.Configuration.Schema;
using NSchema.Import;

namespace NSchema.Cli.Tests.Commands.Import;

public sealed class ImportConfigurationValidatorTests
{
    private readonly ImportConfigurationValidator _sut = new();

    [Fact]
    public void Valid_WithProviderAndImport()
    {
        // Arrange
        var config = new ImportConfiguration
        {
            Provider = new ProviderConfig { Postgres = new PostgresProviderConfig { ConnectionString = "Host=localhost" } },
            Target = new ImportTargetConfig { OutputPath = "./schemas", Format = SchemaFormat.Yaml, Partition = ImportPartitionMode.None },
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
            Target = new ImportTargetConfig { OutputPath = "./schemas", Format = SchemaFormat.Yaml, Partition = ImportPartitionMode.None },
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.ErrorMessage.Contains("database provider is required"));
    }
}
