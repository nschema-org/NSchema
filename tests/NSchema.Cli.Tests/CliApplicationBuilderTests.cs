using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSchema.Cli;
using NSchema.Cli.Configuration;
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
        _configuration.Provider.Type = ProviderType.Postgres;

        // Act
        var act = () => _sut.ConfigureDatabaseProvider();

        // Assert
        Should.Throw<InvalidOperationException>(act).Message.ShouldContain("connection string");
    }

    [Fact]
    public void ConfigureDatabaseProvider_DoesNotThrow_WhenNoProviderConfigured()
    {
        // Arrange
        // Provider.Type is null: offline operations must remain available.

        // Act
        var act = () => _sut.ConfigureDatabaseProvider();

        // Assert
        Should.NotThrow(act);
    }

    [Fact]
    public void ConfigureDatabaseProvider_DoesNotThrow_ForPostgresWithConnectionString()
    {
        // Arrange
        _configuration.Provider.Type = ProviderType.Postgres;
        _configuration.Provider.ConnectionString = "Host=localhost;Database=db";

        // Act
        var act = () => _sut.ConfigureDatabaseProvider();

        // Assert
        Should.NotThrow(act);
    }

    [Theory]
    [InlineData("not-a-uri")]
    [InlineData("s3://")]
    [InlineData("s3://bucket-only")]
    [InlineData("s3://bucket/")]
    [InlineData("s3:///key")]
    public void ConfigureBackendState_Throws_WhenS3ConnectionStringMalformed(string connectionString)
    {
        // Arrange
        _configuration.State.Type = StateType.S3;
        _configuration.State.ConnectionString = connectionString;

        // Act
        var act = () => _sut.ConfigureBackendState();

        // Assert
        Should.Throw<InvalidOperationException>(act).Message.ShouldContain("s3://bucket/key");
    }

    [Fact]
    public void ConfigureBackendState_DoesNotThrow_ForValidS3ConnectionString()
    {
        // Arrange
        _configuration.State.Type = StateType.S3;
        _configuration.State.ConnectionString = "s3://bucket/path/to/state.json";

        // Act
        var act = () => _sut.ConfigureBackendState();

        // Assert
        Should.NotThrow(act);
    }

    [Fact]
    public void ConfigureBackendState_DoesNotThrow_WhenNoConnectionString()
    {
        // Arrange
        // No connection string means online-only: nothing to configure, and no error.

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
        // Scope is empty by default.

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
        _configuration.State.Type = StateType.File;
        _configuration.State.ConnectionString = "./state.json";

        // Act
        using var app = _sut.ConfigureBackendState().Build();

        // Assert
        app.Services.GetService<ISchemaStateStore>().ShouldNotBeNull();
    }

    [Fact]
    public void ConfigureBackendState_RegistersNoStateStore_WhenNoConnectionString()
    {
        // Act
        using var app = _sut.ConfigureBackendState().Build();

        // Assert
        app.Services.GetService<ISchemaStateStore>().ShouldBeNull();
    }
}
