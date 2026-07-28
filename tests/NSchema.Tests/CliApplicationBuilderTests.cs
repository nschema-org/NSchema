using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSchema.Commands;
using NSchema.Configuration.Domain;
using NSchema.Configuration.Plugins;
using NSchema.Configuration.State;
using NSchema.Plan.Policies;
using NSchema.Services.Reporting;
using NSchema.State.Plugins;

namespace NSchema.Tests;

public sealed class CliApplicationBuilderTests
{
    private readonly CliApplicationBuilder _sut = CliApplicationBuilder.Create();

    private static Verbosity ResolvedVerbosity(params string[] args) =>
        ReporterFactory.ResolveVerbosity(RootCommand.Create().Parse(args));

    [Fact]
    public void ResolveVerbosity_DefaultsToNormal() =>
        ResolvedVerbosity("plan").ShouldBe(Verbosity.Normal);

    [Fact]
    public void ResolveVerbosity_Verbose_ResolvesVerbose() =>
        ResolvedVerbosity("plan", "--verbose").ShouldBe(Verbosity.Verbose);

    [Fact]
    public void ResolveVerbosity_Quiet_ResolvesQuiet() =>
        ResolvedVerbosity("plan", "--quiet").ShouldBe(Verbosity.Quiet);

    [Fact]
    public void Parse_QuietAndVerboseTogether_IsAUsageError()
    {
        // Contradictory harness flags are rejected while parsing, before any action runs — see RootCommand.
        var parseResult = RootCommand.Create().Parse(["plan", "--quiet", "--verbose"]);

        parseResult.Errors.ShouldContain(error => error.Message.Contains("--quiet and --verbose cannot be used together"));
    }

    [Fact]
    public void ConfigurePolicies_AppliesDestructiveActionPolicy()
    {
        // Act
        using var app = _sut.ConfigurePolicies(PolicyEnforcement.Warn, null).Build().Require();

        // Assert
        var options = app.Services.GetRequiredService<IOptions<DestructiveActionOptions>>().Value;
        options.Policy.ShouldBe(PolicyEnforcement.Warn);
    }

    [Fact]
    public void ConfigurePolicies_AppliesDataHazardPolicy()
    {
        // Act
        using var app = _sut.ConfigurePolicies(null, PolicyEnforcement.Error).Build().Require();

        // Assert
        var options = app.Services.GetRequiredService<IOptions<DataHazardOptions>>().Value;
        options.Policy.ShouldBe(PolicyEnforcement.Error);
    }

    [Fact]
    public void ConfigurePolicies_LeavesDefaults_WhenPoliciesNull()
    {
        // Act
        using var app = _sut.ConfigurePolicies(null, null).Build().Require();

        // Assert
        app.Services.GetRequiredService<IOptions<DestructiveActionOptions>>().Value
            .Policy.ShouldBe(PolicyEnforcement.Error);
        app.Services.GetRequiredService<IOptions<DataHazardOptions>>().Value
            .Policy.ShouldBe(PolicyEnforcement.Warn);
    }

    [Fact]
    public void ConfigureBackendState_RegistersStateStore_ForFile()
    {
        // Arrange
        var state = new StateConfiguration { File = new FileStateConfiguration { Path = "./state.json" } };

        // Act
        using var app = _sut.ConfigureState(state).Build().Require();

        // Assert
        app.Services.GetService<IDatabaseStateStore>().ShouldNotBeNull();
    }

    [Fact]
    public void ConfigureBackendState_RegistersNoStateStore_WhenNoStoreConfigured()
    {
        // Act
        using var app = _sut.ConfigureState(new StateConfiguration()).Build().Require();

        // Assert
        app.Services.GetService<IDatabaseStateStore>().ShouldBeNull();
    }

    [Fact]
    public void Build_UsesTheSpectreConsolePresenter()
    {
        // Act
        using var app = _sut.Build().Require();

        // Assert — the formatted (non-JSON) builder wires up the Spectre presenter as the CLI's presentation surface.
        app.Presenter.ShouldBeOfType<SpectreConsolePresenter>();
    }

    [Fact]
    public void TryConfigureDatabaseProvider_WithAMisconfiguredProvider_CapturesADiagnosticInsteadOfThrowing()
    {
        // Arrange — a postgres DATABASE statement missing the required connection_string. Loads the real
        // NSchema.Postgres plugin (SDK + network/cache).
        var reference = new PluginReference(new PackageId("NSchema.Postgres"), SemanticVersion.Parse("5.0.0-beta.3"), new PluginLabel("postgres"),
            new PluginSettings(new PluginLabel("postgres"), new Dictionary<string, string?>()));

        // Act
        var result = _sut.TryConfigureDatabase(reference);

        // Assert — captured (not thrown) as a failed Result, its errors labelled with the plugin block.
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldAllBe(error => error.Source == "postgres");
        result.Errors.ShouldContain(error => error.Message.Contains("connection_string", StringComparison.OrdinalIgnoreCase));
    }

    // ConfigureDesiredSchema is a thin delegation to the core's AddProjectSource (which the core tests cover end to
    // end); the CLI-specific logic is which files each glob selects — exercised by ProjectGlobsTests.
}
