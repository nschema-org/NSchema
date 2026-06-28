using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSchema.Commands;
using NSchema.Configuration;
using NSchema.Configuration.Plugins;
using NSchema.Configuration.State;
using NSchema.Diff.Policies;
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
        return app.Services.GetRequiredService<Verbosity>();
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

        app.Services.GetRequiredService<Verbosity>().ShouldBe(Verbosity.Normal);
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
    public void Build_ResolvesTheSpectreConsolePresenter()
    {
        // Act
        using var app = _sut.Build();

        // Assert — the formatted (non-JSON) builder wires up the Spectre presenter as the CLI's presentation surface.
        var presenter = app.Services.GetRequiredService<IConsolePresenter>();
        presenter.ShouldBeOfType<SpectreConsolePresenter>();
    }

    [Fact]
    public void TryConfigureDatabaseProvider_WithAMisconfiguredProvider_CapturesADiagnosticInsteadOfThrowing()
    {
        // doctor's path: a misconfigured plugin must be captured, not thrown, so every problem can be reported. Loads
        // the real NSchema.Postgres plugin (SDK + network/cache). The connection-string env var is cleared so the
        // ambient environment can't satisfy the block under test.
        var savedConnectionString = Environment.GetEnvironmentVariable("NSCHEMA_POSTGRES_CONNECTION_STRING");
        Environment.SetEnvironmentVariable("NSCHEMA_POSTGRES_CONNECTION_STRING", null);
        try
        {
            // Arrange — a postgres PROVIDER block missing the required connection_string.
            var reference = new PluginReference("NSchema.Postgres", "4.0.0-alpha.2", "postgres",
                new ConfigBlock("provider", "postgres", new Dictionary<string, ConfigValue>()));

            // Act
            var diagnostic = _sut.TryConfigureDatabaseProvider(reference);

            // Assert — captured (not thrown), carrying the plugin's own error.
            diagnostic.ShouldNotBeNull();
            diagnostic.Label.ShouldBe("postgres");
            diagnostic.Errors.ShouldContain(error => error.Contains("connection_string", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Environment.SetEnvironmentVariable("NSCHEMA_POSTGRES_CONNECTION_STRING", savedConnectionString);
        }
    }

    // ConfigureDesiredSchema is a thin delegation to the core's AddDdlSchemas (which the core tests cover end to end);
    // the CLI-specific logic is which files each glob selects — exercised by ProjectGlobsTests.
}
