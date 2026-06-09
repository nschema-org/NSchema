using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSchema.Configuration.State;
using NSchema.Diff.Policies;
using NSchema.Operations;
using NSchema.Resolution;
using NSchema.Services;
using NSchema.State;
using Spectre.Console;

namespace NSchema.Tests;

public sealed class CliApplicationBuilderTests
{
    private readonly CliApplicationBuilder _sut = CliApplicationBuilder.Create();

    [Fact]
    public void ConfigurePolicies_AppliesDestructiveActionPolicy()
    {
        // Act
        using var app = _sut.ConfigurePolicies(DestructiveActionPolicy.Warn).Build();

        // Assert
        var options = app.Services.GetRequiredService<IOptions<DestructiveActionOptions>>().Value;
        options.Policy.ShouldBe(DestructiveActionPolicy.Warn);
    }

    [Fact]
    public void ConfigurePolicies_LeavesDefault_WhenPolicyNull()
    {
        // Act
        using var app = _sut.ConfigurePolicies(null).Build();

        // Assert
        var options = app.Services.GetRequiredService<IOptions<DestructiveActionOptions>>().Value;
        options.Policy.ShouldBe(DestructiveActionPolicy.Error);
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
        var reporter = app.Services.GetRequiredService<IKeyedResolver<IOperationReporter>>().Current;
        reporter.ShouldBeOfType<SpectreOperationReporter>();
    }

    [Fact]
    public void ConfigureDesiredSchema_WhenNoSqlFiles_Throws()
    {
        var original = Directory.GetCurrentDirectory();
        var directory = Directory.CreateTempSubdirectory("nschema-noschema-").FullName;
        try
        {
            Directory.SetCurrentDirectory(directory);
            Should.Throw<InvalidOperationException>(() => _sut.ConfigureDesiredSchema())
                .Message.ShouldContain("No schema files");
        }
        finally
        {
            Directory.SetCurrentDirectory(original);
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ConfigureDesiredSchema_FindsSqlFilesRecursively()
    {
        var original = Directory.GetCurrentDirectory();
        var directory = Directory.CreateTempSubdirectory("nschema-schema-").FullName;
        try
        {
            var nested = Directory.CreateDirectory(Path.Combine(directory, "schemas"));
            File.WriteAllText(Path.Combine(nested.FullName, "example.sql"), "CREATE SCHEMA app;");
            Directory.SetCurrentDirectory(directory);

            Should.NotThrow(() => _sut.ConfigureDesiredSchema());
        }
        finally
        {
            Directory.SetCurrentDirectory(original);
            Directory.Delete(directory, recursive: true);
        }
    }
}
