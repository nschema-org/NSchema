using System.CommandLine;
using NSchema.Configuration;
using NSchema.Configuration.Plugins;
using NSchema.Configuration.State;
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

    public CliApplicationBuilder ConfigurePolicies(PolicyEnforcement? destructiveActions, PolicyEnforcement? dataHazards)
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

    public CliApplicationBuilder ConfigureDesiredSchema()
    {
        _builder.AddProjectSource(Directory.GetCurrentDirectory(), ProjectGlobs.Schema());
        return this;
    }

    public CliApplicationBuilder ConfigureState(StateConfiguration? state)
    {
        Throw(TryConfigureState(state));
        return this;
    }

    /// <summary>
    /// Registers the in-memory state store (and its lock) for a run against a disposable database, standing in for
    /// a configured <c>STATE</c> store.
    /// </summary>
    public CliApplicationBuilder ConfigureEphemeralState()
    {
        _builder.UseEphemeralState();
        return this;
    }

    /// <summary>
    /// Configures the run's state store: the ephemeral in-memory store when <paramref name="ephemeral"/> is set
    /// (<c>--ephemeral</c>), otherwise the configured backend.
    /// </summary>
    public CliApplicationBuilder ConfigureState(StateConfiguration? state, bool ephemeral) =>
        ephemeral ? ConfigureEphemeralState() : ConfigureState(state);

    public CliApplicationBuilder ConfigureDatabase(PluginReference? provider)
    {
        Throw(TryConfigureDatabase(provider));
        return this;
    }

    /// <summary>
    /// Configures the database provider WITHOUT throwing: a <see cref="Result"/> that fails, carrying the plugin's
    /// diagnostics, if it could not restore or configure. Success also covers "no provider configured" (a valid
    /// offline setup). The fluent <see cref="ConfigureDatabase"/> is the throwing wrapper; callers that report
    /// failures rather than abort (doctor) use this.
    /// </summary>
    public Result TryConfigureDatabase(PluginReference? provider) =>
        provider is { } reference
            ? TryApply<INSchemaDatabasePlugin>(reference, plugin => plugin.Configure(_builder, reference.Settings))
            : Result.Success();

    /// <summary>
    /// Configures the state backend WITHOUT throwing. The built-in local-file store always succeeds; every other backend
    /// is a plugin, which may fail — a <see cref="Result"/> that fails carrying its diagnostics (success also covers no
    /// backend configured). The fluent <see cref="ConfigureState(NSchema.Configuration.State.StateConfiguration?)"/> is the throwing wrapper.
    /// </summary>
    public Result TryConfigureState(StateConfiguration? state)
    {
        // The local-file store is built into the core and always available; every other backend is a plugin.
        if (state?.File is { } file)
        {
            _builder.UseFileStateStore(file.Path);
            return Result.Success();
        }

        return state?.Plugin is { } reference
            ? TryApply<INSchemaStatePlugin>(reference, plugin => plugin.Configure(_builder, reference.Settings))
            : Result.Success();
    }

    // Configure lives on the two derived interfaces (not the shared base), so the caller supplies the call; this method
    // owns the resolve + non-throwing capture both the throwing wrappers and doctor build on. Resolution and the plugin's
    // own Configure both report failure as data, so there is nothing to catch — the result is composed, not caught.
    private Result TryApply<TPlugin>(PluginReference reference, Func<TPlugin, Result> configure)
        where TPlugin : class, INSchemaPlugin
    {
        var resolved = ResolvePlugin<TPlugin>(reference);
        return resolved.IsFailure
            ? Labelled(reference.Label, resolved.Diagnostics)
            : Labelled(reference.Label, configure(resolved.Value).Diagnostics);
    }

    // A plugin failure is reported under the block label the configuration declares it with (e.g. 'postgres'/'s3') —
    // what the user wrote and can act on — rather than the package id or the plugin's own diagnostic source.
    private static Result Labelled(PluginLabel label, IEnumerable<Diagnostic> diagnostics) =>
        Result.From(diagnostics.Select(diagnostic => diagnostic with { Source = label.Value }));

    private Result<TPlugin> ResolvePlugin<TPlugin>(PluginReference reference) where TPlugin : class, INSchemaPlugin
    {
        var loaded = _plugins.Load(reference.PackageId, reference.Version, _allowRestore);
        if (loaded.IsFailure)
        {
            return Result.Failure<TPlugin>(loaded.Diagnostics);
        }

        var plugin = loaded.Require().OfType<TPlugin>().FirstOrDefault();
        if (plugin is null)
        {
            return Diagnostic.Error(reference.PackageId.Value,
                $"The package '{reference.PackageId}' does not provide a plugin for '{reference.Label}'.");
        }

        return plugin;
    }

    private static void Throw(Result result)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                $"A plugin could not be configured:{Environment.NewLine}{string.Join(Environment.NewLine, result.Errors.Select(e => $"'{e.Source}': {e.Message}"))}");
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
