using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSchema.Cli.Configuration.State;
using NSchema.Cli.Services;
using NSchema.Migration;
using NSchema.Resolution;
using NSchema.State;
using Spectre.Console;

namespace NSchema.Cli.Tests;

public sealed class CliApplicationBuilderTests
{
    private readonly CliApplicationBuilder _sut = CliApplicationBuilder.Create();

    [Fact]
    public void ConfigureScope_SetsSchemaNamesOnMigrationOptions()
    {
        // Act
        using var app = _sut.ConfigureScope(["public", "sales"]).Build();

        // Assert
        var options = app.Services.GetRequiredService<IOptions<MigrationOptions>>().Value;
        options.SchemaNames.ShouldBe(["public", "sales"]);
    }

    [Fact]
    public void ConfigureScope_LeavesSchemaNamesUnset_WhenScopeNull()
    {
        // Act
        using var app = _sut.ConfigureScope(null).Build();

        // Assert
        var options = app.Services.GetRequiredService<IOptions<MigrationOptions>>().Value;
        options.SchemaNames.ShouldBeNull();
    }

    [Fact]
    public void ConfigurePolicies_AppliesDestructiveActionPolicy()
    {
        // Act
        using var app = _sut.ConfigurePolicies(DestructiveActionPolicy.Warn).Build();

        // Assert
        var options = app.Services.GetRequiredService<IOptions<MigrationOptions>>().Value;
        options.DestructiveActionPolicy.ShouldBe(DestructiveActionPolicy.Warn);
    }

    [Fact]
    public void ConfigurePolicies_LeavesDefault_WhenPolicyNull()
    {
        // Act
        using var app = _sut.ConfigurePolicies(null).Build();

        // Assert
        var options = app.Services.GetRequiredService<IOptions<MigrationOptions>>().Value;
        options.DestructiveActionPolicy.ShouldBe(DestructiveActionPolicy.Error);
    }

    [Fact]
    public void ConfigureBackendState_RegistersStateStore_ForFile()
    {
        // Arrange
        var state = new StateConfig { File = new FileStateConfig { Path = "./state.json" } };

        // Act
        using var app = _sut.ConfigureBackendState(state).Build();

        // Assert
        app.Services.GetService<ISchemaStateStore>().ShouldNotBeNull();
    }

    [Fact]
    public void ConfigureBackendState_RegistersNoStateStore_WhenNoStoreConfigured()
    {
        // Act
        using var app = _sut.ConfigureBackendState(new StateConfig()).Build();

        // Assert
        app.Services.GetService<ISchemaStateStore>().ShouldBeNull();
    }

    [Fact]
    public void Build_RegistersAnAnsiConsole()
    {
        // Act
        using var app = _sut.Build();

        // Assert
        app.Services.GetService<IAnsiConsole>().ShouldNotBeNull();
    }

    [Fact]
    public void Build_ResolvesTheSpectreReporter_WithoutDuplicateFormatCollision()
    {
        // Act — a second reporter sharing the core "human" format would throw at resolution; the Spectre reporter
        // is registered under its own format and selected, so the default coexists harmlessly.
        using var app = _sut.Build();

        // Assert
        var reporter = app.Services.GetRequiredService<IKeyedResolver<IMigrationReporter>>().Current;
        reporter.ShouldBeOfType<SpectreMigrationReporter>();
    }
}
