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
