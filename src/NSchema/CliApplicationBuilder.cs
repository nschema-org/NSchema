using System.CommandLine;
using NSchema.Configuration;
using NSchema.Configuration.Plugins;
using NSchema.Configuration.State;
using NSchema.Diagnostics;
using NSchema.Diff.Policies;
using NSchema.Policies;
using NSchema.Plugins;
using NSchema.Services.Reporting;

namespace NSchema;

internal sealed class CliApplicationBuilder
{
    private readonly NSchemaApplicationBuilder _builder;
    private readonly PluginLoader _plugins = new();
    private readonly bool _allowRestore;
    private readonly IConsoleMessenger _messenger;
    private readonly IConsolePresenter _presenter;

    private CliApplicationBuilder(OutputFormat format, Verbosity verbosity, bool allowRestore)
    {
        _allowRestore = allowRestore;
        _builder = NSchemaApplication.CreateBuilder();

        // The messenger and presenter are stateless console utilities, so the CLI owns them directly (see CliApplication)
        // rather than registering them in the container. The engine still narrates progress through its own seam, so
        // feed that one the messenger.
        _messenger = ReporterFactory.CreateMessenger(format, verbosity);
        _presenter = ReporterFactory.CreatePresenter(format);
        _builder.UseProgressReporter(new ConsoleProgress(_messenger));
    }

    public CliApplicationBuilder ConfigurePolicies(DestructiveActionPolicy? destructiveActions, PolicyEnforcement? dataHazards)
    {
        if (destructiveActions is { } destructive)
        {
            _builder.WithDestructiveActionPolicy(destructive);
        }

        if (dataHazards is { } hazards)
        {
            _builder.WithDataHazardPolicy(hazards);
        }

        return this;
    }

    public CliApplicationBuilder ConfigureDesiredSchema(string? environment)
    {
        var root = Directory.GetCurrentDirectory();
        _builder.AddDdlSchemas(root, ProjectGlobs.BaseSchema());

        // Layer the selected environment's overlay files on top.
        if (environment is not null)
        {
            _builder.AddDdlSchemas(root, ProjectGlobs.EnvironmentSchema(environment));
        }

        return this;
    }

    public CliApplicationBuilder ConfigureBackendState(StateConfig? state)
    {
        Throw(TryConfigureBackendState(state));
        return this;
    }

    public CliApplicationBuilder ConfigureDatabaseProvider(PluginReference? provider)
    {
        Throw(TryConfigureDatabaseProvider(provider));
        return this;
    }

    /// <summary>
    /// Configures the database provider WITHOUT throwing: applies it on success, or returns a
    /// <see cref="PluginDiagnostic"/> if it failed to restore or configure. Returns <see langword="null"/> when it
    /// succeeds or no provider is configured (a valid offline setup). The fluent <see cref="ConfigureDatabaseProvider"/>
    /// is the throwing wrapper; callers that want to report failures rather than abort (doctor) use this.
    /// </summary>
    public PluginDiagnostic? TryConfigureDatabaseProvider(PluginReference? provider) =>
        provider is { } reference
            ? TryApply<INSchemaProviderPlugin>(reference, plugin => plugin.Configure(_builder, reference.Block))
            : null;

    /// <summary>
    /// Configures the state backend WITHOUT throwing. The built-in local-file store always succeeds; every other backend
    /// is a plugin, which may fail — returning a <see cref="PluginDiagnostic"/> (<see langword="null"/> on success or
    /// when no backend is configured). The fluent <see cref="ConfigureBackendState"/> is the throwing wrapper.
    /// </summary>
    public PluginDiagnostic? TryConfigureBackendState(StateConfig? state)
    {
        // The local-file store is built into the core and always available; every other backend is a plugin.
        if (state?.File is { } file)
        {
            _builder.UseFileStateStore(file.Path);
            return null;
        }

        return state?.Plugin is { } reference
            ? TryApply<INSchemaBackendPlugin>(reference, plugin => plugin.Configure(_builder, reference.Block))
            : null;
    }

    // Configure lives on the two derived interfaces (not the shared base), so the caller supplies the call; this method
    // owns the resolve + non-throwing capture both the throwing wrappers and doctor build on. Resolution and the plugin's
    // own Configure both report failure as data now, so there is nothing to catch — the diagnostic is composed, not caught.
    private PluginDiagnostic? TryApply<TPlugin>(PluginReference reference, Func<TPlugin, PluginConfigureResult> configure)
        where TPlugin : class, INSchemaPlugin
    {
        var resolved = ResolvePlugin<TPlugin>(reference);
        if (resolved.IsFailure)
        {
            return new PluginDiagnostic(reference.Label, resolved.Errors.Select(e => e.Message).ToList());
        }

        var result = configure(resolved.Value);
        return result.Succeeded ? null : new PluginDiagnostic(reference.Label, result.Errors);
    }

    private Result<TPlugin> ResolvePlugin<TPlugin>(PluginReference reference) where TPlugin : class, INSchemaPlugin
    {
        var loaded = _plugins.Load(reference.PackageId, reference.Version, _allowRestore);
        if (loaded.IsFailure)
        {
            return Result.Failure<TPlugin>(loaded.Diagnostics);
        }

        var plugin = loaded.Value.OfType<TPlugin>().FirstOrDefault(p => string.Equals(p.Label, reference.Label, StringComparison.OrdinalIgnoreCase));
        if (plugin is null)
        {
            return Diagnostic.Error(reference.PackageId,
                $"The package '{reference.PackageId}' does not provide a plugin for '{reference.Label}'.");
        }

        return plugin;
    }

    private static void Throw(PluginDiagnostic? diagnostic)
    {
        if (diagnostic is { } problem)
        {
            throw new InvalidOperationException(
                $"The '{problem.Label}' plugin could not be configured:{Environment.NewLine}{string.Join(Environment.NewLine, problem.Errors)}");
        }
    }

    public CliApplication Build() => new(_builder.Build(), _messenger, _presenter);

    /// <summary>
    /// Creates a builder rendering formatted (text) output at the default verbosity.
    /// </summary>
    public static CliApplicationBuilder Create() => new(OutputFormat.Text, Verbosity.Normal, allowRestore: true);

    /// <summary>
    /// Creates a builder whose output format and verbosity follow the command-line flags.
    /// </summary>
    public static CliApplicationBuilder Create(ParseResult parseResult) =>
        new(ReporterFactory.ResolveFormat(parseResult), ReporterFactory.ResolveVerbosity(parseResult),
            allowRestore: !CommonOptions.NoInit.GetValueOrDefault(parseResult, false));
}
