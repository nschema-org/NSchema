using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using NSchema.Configuration;
using NSchema.Configuration.Plugins;
using NSchema.Configuration.State;
using NSchema.Diff.Policies;
using NSchema.Operations.Confirmation;
using NSchema.Plugins;
using NSchema.Services;
using Spectre.Console;

namespace NSchema;

internal sealed class CliApplicationBuilder
{
    private readonly NSchemaApplicationBuilder _builder;
    private readonly PluginLoader _plugins = new();
    private readonly bool _allowRestore;

    private CliApplicationBuilder(bool json, Verbosity verbosity, bool allowRestore)
    {
        _allowRestore = allowRestore;
        _builder = NSchemaApplication.CreateBuilder(new NSchemaApplicationOptions
        {
            ExceptionBehavior = ExceptionBehavior.Throw,
        });

        _builder.UseTerraformRenderer(o => o.IncludeColour = false);

        if (json)
        {
            _builder.UseReporter<JsonOperationReporter>();
        }
        else
        {
            _builder.UseReporter<SpectreOperationReporter>();
        }

        _builder.Services.AddSingleton(AnsiConsole.Console);
        _builder.Services.AddSingleton<RunOutcome>();
        _builder.Services.AddSingleton(new OutputVerbosity(verbosity));
    }

    public CliApplicationBuilder ConfigurePolicies(DestructiveActionPolicy? policy)
    {
        if (policy is { } value)
        {
            _builder.WithDestructiveActionPolicy(value);
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
    // owns the resolve + non-throwing capture both the throwing wrappers and doctor build on.
    private PluginDiagnostic? TryApply<TPlugin>(PluginReference reference, Func<TPlugin, PluginConfigureResult> configure)
        where TPlugin : class, INSchemaPlugin
    {
        try
        {
            var result = configure(ResolvePlugin<TPlugin>(reference));
            return result.Succeeded ? null : new PluginDiagnostic(reference.Label, result.Errors);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A restore/load failure (bad version pin, missing plugin, Core-major mismatch) is just as much a
            // diagnostic as a Configure failure — capture it rather than letting it propagate raw.
            return new PluginDiagnostic(reference.Label, [ex.Message]);
        }
    }

    private TPlugin ResolvePlugin<TPlugin>(PluginReference reference) where TPlugin : class, INSchemaPlugin
    {
        var plugins = _plugins.Load(reference.PackageId, reference.Version, _allowRestore);
        return plugins.OfType<TPlugin>().FirstOrDefault(p => string.Equals(p.Label, reference.Label, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                $"The package '{reference.PackageId}' does not provide a plugin for '{reference.Label}'.");
    }

    private static void Throw(PluginDiagnostic? diagnostic)
    {
        if (diagnostic is { } problem)
        {
            throw new InvalidOperationException(
                $"The '{problem.Label}' plugin could not be configured:{Environment.NewLine}{string.Join(Environment.NewLine, problem.Errors)}");
        }
    }

    public CliApplicationBuilder ConfigureConfirmation(bool autoApprove)
    {
        _builder.Services.AddSingleton<IOperationConfirmation>(sp => new ConsoleOperationConfirmation(autoApprove, sp.GetRequiredService<IAnsiConsole>()));
        return this;
    }

    public NSchemaApplication Build() => _builder.Build();

    /// <summary>
    /// Creates a builder rendering formatted (text) output at the default verbosity.
    /// </summary>
    public static CliApplicationBuilder Create() => new(json: false, Verbosity.Normal, allowRestore: true);

    /// <summary>
    /// Creates a builder whose output format and verbosity follow the command-line flags.
    /// </summary>
    public static CliApplicationBuilder Create(ParseResult parseResult) =>
        new(CommonOptions.Json.GetValueOrDefault(parseResult, false), ResolveVerbosity(parseResult),
            allowRestore: !CommonOptions.NoInit.GetValueOrDefault(parseResult, false));

    /// <summary>
    /// Resolves <c>--quiet</c> / <c>--verbose</c> to a single <see cref="Verbosity"/>. The two flags are mutually
    /// exclusive: passing both is a usage error rather than a silent precedence.
    /// </summary>
    private static Verbosity ResolveVerbosity(ParseResult parseResult)
    {
        var quiet = CommonOptions.Quiet.GetValueOrDefault(parseResult, false);
        var verbose = CommonOptions.Verbose.GetValueOrDefault(parseResult, false);

        if (quiet && verbose)
        {
            throw new InvalidOperationException("--quiet and --verbose cannot be used together.");
        }

        return verbose ? Verbosity.Verbose : quiet ? Verbosity.Quiet : Verbosity.Normal;
    }
}
