using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSchema.Commands;
using NSchema.Configuration.State;
using NSchema.Diff.Policies;
using NSchema.Operations;
using NSchema.Services;
using NSchema.State;
using Spectre.Console;

namespace NSchema.Tests;

public sealed class CliApplicationBuilderTests
{
    private readonly CliApplicationBuilder _sut = CliApplicationBuilder.Create();

    private static Verbosity ResolvedVerbosity(params string[] args)
    {
        var parseResult = RootCommand.Create().Parse(args);
        using var app = CliApplicationBuilder.Create(parseResult).Build();
        return app.Services.GetRequiredService<OutputVerbosity>().Level;
    }

    [Fact]
    public void Create_DefaultsToNormalVerbosity() =>
        ResolvedVerbosity("plan").ShouldBe(Verbosity.Normal);

    [Fact]
    public void Create_Verbose_ResolvesVerboseVerbosity() =>
        ResolvedVerbosity("plan", "--verbose").ShouldBe(Verbosity.Verbose);

    [Fact]
    public void Create_Quiet_ResolvesQuietVerbosity() =>
        ResolvedVerbosity("plan", "--quiet").ShouldBe(Verbosity.Quiet);

    [Fact]
    public void Create_QuietAndVerboseTogether_Throws()
    {
        var parseResult = RootCommand.Create().Parse(["plan", "--quiet", "--verbose"]);

        var ex = Should.Throw<InvalidOperationException>(() => CliApplicationBuilder.Create(parseResult));
        ex.Message.ShouldContain("--quiet and --verbose cannot be used together");
    }

    [Fact]
    public void Create_NoArg_DefaultsToNormalVerbosity()
    {
        using var app = CliApplicationBuilder.Create().Build();

        app.Services.GetRequiredService<OutputVerbosity>().Level.ShouldBe(Verbosity.Normal);
    }

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
        var reporter = app.Services.GetRequiredService<IOperationReporter>();
        reporter.ShouldBeOfType<SpectreOperationReporter>();
    }

    // ConfigureDesiredSchema is a thin delegation to the core's AddDdlSchemas (which the core tests cover end to end);
    // the CLI-specific logic is which files each glob selects — exercised by ProjectGlobsTests.
}
