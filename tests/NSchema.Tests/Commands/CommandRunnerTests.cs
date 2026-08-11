using FluentValidation;
using NSchema.Commands;
using NSchema.Commands.Plan;
using NSchema.Configuration.Domain;
using NSchema.Configuration.Plugins;
using NSchema.Configuration.State;
using RootCommand = NSchema.Commands.RootCommand;

namespace NSchema.Tests.Commands;

/// <summary>
/// The preamble every configured command shares. What matters is the order it stops in: a configuration failure must
/// never reach the builder, and a build failure must never reach the command's body.
/// </summary>
public sealed class CommandRunnerTests : IDisposable
{
    private readonly string _originalDirectory = Directory.GetCurrentDirectory();
    private readonly string _projectDirectory = Directory.CreateTempSubdirectory("nschema-runner-").FullName;

    // The runner applies --directory by changing the process working directory; restore it so tests stay hermetic.
    public void Dispose()
    {
        Directory.SetCurrentDirectory(_originalDirectory);
        Directory.Delete(_projectDirectory, recursive: true);
    }

    private Task WriteConfiguration(string sql) =>
        File.WriteAllTextAsync(Path.Combine(_projectDirectory, "config.sql"), sql, TestContext.Current.CancellationToken);

    private Task<int> Run(
        Func<CliApplicationBuilder, PlanConfiguration, CliApplicationBuilder> configure,
        Func<CommandContext<PlanConfiguration>, CancellationToken, Task<int>> run,
        IValidator<PlanConfiguration>? validator = null,
        params string[] args) =>
        CommandRunner.Run(
            RootCommand.Create().Parse(["plan", "--directory", _projectDirectory, .. args]),
            configure,
            run,
            validator, true, TestContext.Current.CancellationToken);

    [Fact]
    public async Task Run_HandsTheValidatedConfigurationAndBuiltApplicationToTheBody()
    {
        // Arrange
        await WriteConfiguration("STATE file ( path = './state.json' );");
        CommandContext<PlanConfiguration>? seen = null;

        // Act
        var exitCode = await Run(
            (builder, configuration) => builder.ConfigureState(configuration.State),
            (context, _) =>
            {
                seen = context;
                return Task.FromResult(ExitCodes.NoChanges);
            });

        // Assert
        exitCode.ShouldBe(ExitCodes.NoChanges);
        seen.ShouldNotBeNull();
        seen.App.ShouldNotBeNull();
        seen.Configuration.State!.File!.Path.ShouldBe("./state.json");
    }

    [Fact]
    public async Task Run_ConfigurationFailure_StopsBeforeConfiguringOrRunning()
    {
        // Arrange — a DATABASE naming a plugin no PLUGIN statement declares.
        await WriteConfiguration("DATABASE postgres ( connection_string = 'x' );");
        var configured = false;
        var ran = false;

        // Act
        var exitCode = await Run(
            (builder, _) => { configured = true; return builder; },
            (_, _) => { ran = true; return Task.FromResult(ExitCodes.NoChanges); });

        // Assert
        exitCode.ShouldBe(ExitCodes.Error);
        configured.ShouldBeFalse();
        ran.ShouldBeFalse();
    }

    [Fact]
    public async Task Run_ValidationFailure_StopsBeforeConfiguringOrRunning()
    {
        // Arrange — plan requires a current-schema source; an empty project satisfies neither.
        await WriteConfiguration("-- nothing configured");
        var configured = false;
        var ran = false;

        // Act
        var exitCode = await Run(
            (builder, _) => { configured = true; return builder; },
            (_, _) => { ran = true; return Task.FromResult(ExitCodes.NoChanges); },
            new PlanConfigurationValidator());

        // Assert
        exitCode.ShouldBe(ExitCodes.Error);
        configured.ShouldBeFalse();
        ran.ShouldBeFalse();
    }

    [Fact]
    public async Task Run_BuildFailure_StopsBeforeRunning()
    {
        // Arrange — a plugin that is not in the cache, with --no-init forbidding a restore. That fails the build rather
        // than the configuration load, and needs no network to do it.
        await WriteConfiguration("STATE file ( path = './state.json' );");
        var label = new PluginLabel("nope");
        var reference = new PluginReference(
            new PackageId("Acme.Nonexistent.Plugin"),
            new ResolvedPackage(SemanticVersion.Parse("1.0.0")),
            label,
            new PluginSettings(label, new Dictionary<string, string?>()));
        var ran = false;

        // Act
        var exitCode = await Run(
            (builder, _) => builder.ConfigureState(new StateConfiguration { Plugin = reference }),
            (_, _) => { ran = true; return Task.FromResult(ExitCodes.NoChanges); },
            args: "--no-init");

        // Assert
        exitCode.ShouldBe(ExitCodes.Error);
        ran.ShouldBeFalse();
    }

    // A PLUGIN declared by path, which is resolved while the configuration is read and reports an advisory finding
    // for being one. Only the files' existence is checked there, and this configuration never loads the plugin, so
    // two placeholder files stand in for a build.
    private async Task WritePathPluginConfiguration()
    {
        var assembly = Path.Combine(_projectDirectory, "Acme.Test.Plugin.dll");
        await File.WriteAllTextAsync(assembly, "", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.ChangeExtension(assembly, ".deps.json"), "{}", TestContext.Current.CancellationToken);

        await WriteConfiguration(
            $"""
             PLUGIN db ( path = '{assembly}' );
             DATABASE db ( connection_string = 'x' );
             STATE file ( path = './state.json' );
             """);
    }

    private Task WriteEditorConfig(string severities) =>
        File.WriteAllTextAsync(
            Path.Combine(_projectDirectory, ".editorconfig"),
            $"root = true{Environment.NewLine}{Environment.NewLine}[*]{Environment.NewLine}{severities}{Environment.NewLine}",
            TestContext.Current.CancellationToken);

    // Whether a finding was reported is not visible in an exit code, and silencing an advisory one changes nothing
    // else — so these two read the report itself, through --json because it is the format meant to be parsed.
    private async Task<string> RunCapturingReport()
    {
        var original = Console.Out;
        var captured = new StringWriter();
        Console.SetOut(captured);

        try
        {
            await Run(
                (builder, _) => builder,
                (_, _) => Task.FromResult(ExitCodes.NoChanges),
                args: "--json");
        }
        finally
        {
            Console.SetOut(original);
        }

        return captured.ToString();
    }

    [Fact]
    public async Task Run_AdvisoryConfigurationFinding_IsReportedAndDoesNotStopTheCommand()
    {
        // Arrange — the control for the three tests below: left alone, this finding is reported and only advises.
        await WritePathPluginConfiguration();
        var ran = false;

        // Act
        var report = await RunCapturingReport();
        var exitCode = await Run(
            (builder, _) => builder,
            (_, _) => { ran = true; return Task.FromResult(ExitCodes.NoChanges); });

        // Assert
        report.ShouldContain("plugin-from-path");
        exitCode.ShouldBe(ExitCodes.NoChanges);
        ran.ShouldBeTrue();
    }

    [Fact]
    public async Task Run_EditorConfigRaisingAConfigurationFinding_StopsBeforeRunning()
    {
        // Arrange — reading the project mints this finding before an engine exists to hold a policy, so the runner
        // rather than the engine has to enforce the project's .editorconfig over it.
        await WritePathPluginConfiguration();
        await WriteEditorConfig("nschema_diagnostic.plugin-from-path.severity = error");
        var ran = false;

        // Act
        var exitCode = await Run(
            (builder, _) => builder,
            (_, _) => { ran = true; return Task.FromResult(ExitCodes.NoChanges); });

        // Assert — raising it has to stop the command, not merely print it in red.
        exitCode.ShouldBe(ExitCodes.Error);
        ran.ShouldBeFalse();
    }

    [Fact]
    public async Task Run_EditorConfigSilencingAConfigurationFinding_LeavesItUnreported()
    {
        // Arrange — by source rather than by code, which is the other half of the enforcement the runner applies.
        await WritePathPluginConfiguration();
        await WriteEditorConfig("nschema_diagnostic_source.plugins.severity = none");

        // Act
        var report = await RunCapturingReport();

        // Assert
        report.ShouldNotContain("plugin-from-path");
    }

    [Fact]
    public async Task Run_EditorConfigSilencingAStructuralConfigurationFinding_StillStops()
    {
        // Arrange — a DATABASE naming a plugin no PLUGIN statement declares, which the configuration cannot be read
        // without. Enforcement may lower a finding that advises; one the read depends on is refused, or a project
        // could configure its way out of being unreadable.
        await WriteConfiguration("DATABASE postgres ( connection_string = 'x' );");
        await WriteEditorConfig("nschema_diagnostic.unknown-plugin-label.severity = none");
        var ran = false;

        // Act
        var exitCode = await Run(
            (builder, _) => builder,
            (_, _) => { ran = true; return Task.FromResult(ExitCodes.NoChanges); });

        // Assert
        exitCode.ShouldBe(ExitCodes.Error);
        ran.ShouldBeFalse();
    }

    [Fact]
    public async Task Run_WithoutAValidator_SkipsValidationEntirely()
    {
        // Arrange — the same empty project the validator rejects above.
        await WriteConfiguration("-- nothing configured");
        var ran = false;

        // Act
        var exitCode = await Run(
            (builder, _) => builder,
            (_, _) => { ran = true; return Task.FromResult(ExitCodes.NoChanges); });

        // Assert
        exitCode.ShouldBe(ExitCodes.NoChanges);
        ran.ShouldBeTrue();
    }
}
