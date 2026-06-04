using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSchema.Cli.Configuration;
using NSchema.Cli.Configuration.Provider;
using NSchema.Cli.Configuration.State;
using NSchema.Migration;
using NSchema.State;

namespace NSchema.Cli.Tests;

public sealed class CliApplicationBuilderTests
{
    private readonly NSchemaConfiguration _configuration = new();
    private readonly CliApplicationBuilder _sut;

    public CliApplicationBuilderTests()
    {
        // The builder holds the configuration by reference, so tests adjust _configuration in their Arrange step.
        _sut = CliApplicationBuilder.Create(_configuration);
    }

    [Fact]
    public void ConfigureDesiredSchema_Throws_WhenNoDirectoryConfigured()
    {
        // Arrange
        // Schema.Directory is null by default.

        // Act
        var act = () => _sut.ConfigureDesiredSchema();

        // Assert
        Should.Throw<InvalidOperationException>(act).Message.ShouldContain("schema directory");
    }

    [Fact]
    public void ConfigureDatabaseProvider_Throws_WhenPostgresHasNoConnectionString()
    {
        // Arrange
        _configuration.Provider.Postgres = new PostgresProviderConfig();

        // Act
        var act = () => _sut.ConfigureDatabaseProvider();

        // Assert
        Should.Throw<InvalidOperationException>(act).Message.ShouldContain("connectionString");
    }

    [Fact]
    public void ConfigureDatabaseProvider_DoesNotThrow_WhenNoProviderConfigured()
    {
        // Arrange
        // No provider section is set: offline operations must remain available.

        // Act
        var act = () => _sut.ConfigureDatabaseProvider();

        // Assert
        Should.NotThrow(act);
    }

    [Fact]
    public void ConfigureDatabaseProvider_DoesNotThrow_ForPostgresWithConnectionString()
    {
        // Arrange
        _configuration.Provider.Postgres = new PostgresProviderConfig { ConnectionString = "Host=localhost;Database=db" };

        // Act
        var act = () => _sut.ConfigureDatabaseProvider();

        // Assert
        Should.NotThrow(act);
    }

    [Fact]
    public void ConfigureBackendState_Throws_WhenS3SectionIncomplete()
    {
        // Arrange
        _configuration.State.S3 = new S3StateConfig { Bucket = "bucket" };

        // Act
        var act = () => _sut.ConfigureBackendState();

        // Assert
        Should.Throw<InvalidOperationException>(act).Message.ShouldContain("state.s3.key");
    }

    [Fact]
    public void ConfigureBackendState_Throws_WhenMoreThanOneStoreConfigured()
    {
        // Arrange
        _configuration.State.File = new FileStateConfig { Path = "./state.json" };
        _configuration.State.S3 = new S3StateConfig { Bucket = "bucket", Key = "key" };

        // Act
        var act = () => _sut.ConfigureBackendState();

        // Assert
        Should.Throw<InvalidOperationException>(act).Message.ShouldContain("exactly one");
    }

    [Fact]
    public void ConfigureBackendState_DoesNotThrow_ForValidS3Store()
    {
        // Arrange
        _configuration.State.S3 = new S3StateConfig { Bucket = "bucket", Key = "path/to/state.json" };

        // Act
        var act = () => _sut.ConfigureBackendState();

        // Assert
        Should.NotThrow(act);
    }

    [Fact]
    public void ConfigureBackendState_DoesNotThrow_WhenNoStoreConfigured()
    {
        // Arrange
        // No store section means online-only: nothing to configure, and no error.

        // Act
        var act = () => _sut.ConfigureBackendState();

        // Assert
        Should.NotThrow(act);
    }

    [Fact]
    public void ConfigureScope_SetsSchemaNamesOnMigrationOptions()
    {
        // Arrange
        _configuration.Scope = ["public", "sales"];

        // Act
        using var app = _sut.ConfigureScope().Build();

        // Assert
        var options = app.Services.GetRequiredService<IOptions<MigrationOptions>>().Value;
        options.SchemaNames.ShouldBe(["public", "sales"]);
    }

    [Fact]
    public void ConfigureScope_LeavesSchemaNamesUnset_WhenScopeEmpty()
    {
        // Arrange
        // Scope is unset (null) by default.

        // Act
        using var app = _sut.ConfigureScope().Build();

        // Assert
        var options = app.Services.GetRequiredService<IOptions<MigrationOptions>>().Value;
        options.SchemaNames.ShouldBeNull();
    }

    [Fact]
    public void ConfigurePolicies_AppliesDestructiveActionPolicy()
    {
        // Arrange
        _configuration.DestructiveActionPolicy = DestructiveActionPolicy.Warn;

        // Act
        using var app = _sut.ConfigurePolicies().Build();

        // Assert
        var options = app.Services.GetRequiredService<IOptions<MigrationOptions>>().Value;
        options.DestructiveActionPolicy.ShouldBe(DestructiveActionPolicy.Warn);
    }

    [Fact]
    public void ConfigureBackendState_RegistersStateStore_ForFile()
    {
        // Arrange
        _configuration.State.File = new FileStateConfig { Path = "./state.json" };

        // Act
        using var app = _sut.ConfigureBackendState().Build();

        // Assert
        app.Services.GetService<ISchemaStateStore>().ShouldNotBeNull();
    }

    [Fact]
    public void ConfigureBackendState_RegistersNoStateStore_WhenNoStoreConfigured()
    {
        // Act
        using var app = _sut.ConfigureBackendState().Build();

        // Assert
        app.Services.GetService<ISchemaStateStore>().ShouldBeNull();
    }
}
