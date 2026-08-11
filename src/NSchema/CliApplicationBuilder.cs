using System.CommandLine;
using NSchema.Configuration;
using NSchema.Configuration.Plugins;
using NSchema.Configuration.State;
using NSchema.Plugins;

namespace NSchema;

internal sealed class CliApplicationBuilder
{
    private readonly NSchemaApplicationBuilder _builder;
    private readonly bool _allowRestore;
    private readonly string? _environment;
    private readonly IConsoleReporter _reporter;
    private readonly DiagnosticCollection _diagnostics = [];
    private PolicyEnforcement? _destructiveActions;
    private PolicyEnforcement? _dataHazards;

    private CliApplicationBuilder(OutputFormat format, Verbosity verbosity, bool allowRestore, string? environment)
    {
        _allowRestore = allowRestore;
        _environment = environment;
        _builder = NSchemaApplication.CreateBuilder();
        _reporter = ReporterFactory.CreateReporter(format, verbosity);
        _builder.UseProgressReporter(new ConsoleProgress(_reporter));
    }

    // Lazy: the loader anchors at the project directory, which --directory establishes before first use.
    private PluginLoader Plugins => field ??= new PluginLoader(Directory.GetCurrentDirectory());

    public CliApplicationBuilder ConfigurePolicies(PolicyEnforcement? destructiveActions, PolicyEnforcement? dataHazards)
    {
        _destructiveActions = destructiveActions;
        _dataHazards = dataHazards;
        return this;
    }

    /// <summary>
    /// Registers the files the desired schema is read from: the base set, plus the selected environment's overlay.
    /// </summary>
    public CliApplicationBuilder ConfigureDesiredSchema()
    {
        var root = Directory.GetCurrentDirectory();
        _builder.AddProjectSource(root, ProjectGlobs.Base());

        // Registered second so the overlay layers after the base, matching how its configuration statements resolve.
        if (_environment != null)
        {
            _builder.AddProjectSource(root, ProjectGlobs.Environment(_environment));
        }

        return this;
    }

    public CliApplicationBuilder ConfigureState(StateConfiguration? state)
    {
        _diagnostics.AddRange(TryConfigureState(state).Diagnostics);
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
        _diagnostics.AddRange(TryConfigureDatabase(provider).Diagnostics);
        return this;
    }

    /// <summary>
    /// Configures the database provider.
    /// </summary>
    public Result TryConfigureDatabase(PluginReference? provider) =>
        provider is { } reference
            ? TryApply<INSchemaDatabasePlugin>(reference, plugin => plugin.Configure(_builder, reference.Settings))
            : Result.Success();

    /// <summary>
    /// Configures the state backend.
    /// </summary>
    public Result TryConfigureState(StateConfiguration? state)
    {
        // The local-file store is built into the core and always available; every other backend is a plugin.
        if (state?.File is { } file)
        {
            _builder.UseFileState(file.Path);
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
        if (resolved.IsFailure)
        {
            return Labelled(reference.Label, resolved.Diagnostics);
        }

        try
        {
            return Labelled(reference.Label, configure(resolved.Value).Diagnostics);
        }
        catch (Exception ex) when (ex is TypeLoadException or MissingMemberException)
        {
            // The plugin loaded, but a member it calls is missing from this host's NSchema.Core — the same version
            // skew the loader rejects at instantiation, surfacing here because the runtime binds a member on first
            // call. Reported like any other unloadable plugin, so doctor can say so instead of the CLI crashing.
            return Labelled(reference.Label,
                [PluginDiagnostics.IncompatiblePlugin(reference.PackageId, reference.Version, ex.Message)]);
        }
    }

    // A plugin failure is prefixed with the block label the configuration declares it with (e.g. 'postgres'/'s3') —
    // what the user wrote and can act on, rather than the package id. It rides in the message rather than replacing
    // the source, because a source says what produced a finding and the label is neither producer nor code.
    private static Result Labelled(PluginLabel label, IEnumerable<Diagnostic> diagnostics) =>
        Result.From(diagnostics.Select(diagnostic => diagnostic with { Text = $"{label}: {diagnostic.Text}" }));

    private Result<TPlugin> ResolvePlugin<TPlugin>(PluginReference reference) where TPlugin : class, INSchemaPlugin
    {
        var loaded = reference.Origin switch
        {
            ResolvedPath path => Plugins.LoadFromPath(reference.PackageId, path.AssemblyPath),
            ResolvedPackage package => Plugins.Load(reference.PackageId, package.Version, _allowRestore),
            _ => throw new NotSupportedException($"Unknown plugin origin '{reference.Origin.GetType().Name}'."),
        };
        if (loaded.IsFailure)
        {
            return Result.Failure<TPlugin>(loaded.Diagnostics);
        }

        var plugin = loaded.Require().OfType<TPlugin>().FirstOrDefault();
        if (plugin is null)
        {
            return PluginDiagnostics.MissingCapability(reference.PackageId);
        }

        return plugin;
    }

    /// <summary>
    /// Builds the application, or fails carrying the diagnostics the configuration steps accumulated.
    /// </summary>
    public Result<CliApplication> Build()
    {
        ApplyDiagnosticSeverities();

        return _diagnostics.HasErrors
            ? Result.Failure<CliApplication>(_diagnostics)
            : Result.From(new CliApplication(_builder.Build(), _reporter), _diagnostics);
    }

    /// <remarks>
    /// Applies diagnostic overrides in precedence order.
    /// </remarks>
    private void ApplyDiagnosticSeverities()
    {
        var read = EditorConfigReader.Read(Directory.GetCurrentDirectory(), _environment);
        _diagnostics.AddRange(read.Diagnostics);
        var overrides = read.Value ?? DiagnosticOverrides.None;

        foreach (var (code, enforcement) in overrides.ByCode)
        {
            _builder.WithDiagnostic(code, enforcement);
        }

        foreach (var (source, enforcement) in overrides.BySource)
        {
            _builder.WithDiagnosticsFrom(source, enforcement);
        }

        if (_destructiveActions is { } destructive)
        {
            _builder.WithDestructiveActions(destructive);
        }

        if (_dataHazards is { } hazards)
        {
            _builder.WithDataHazards(hazards);
        }
    }

    /// <summary>
    /// Creates a builder rendering formatted (text) output at the default verbosity.
    /// </summary>
    public static CliApplicationBuilder Create() =>
        new(OutputFormat.Text, Verbosity.Normal, allowRestore: true, environment: null);

    /// <summary>
    /// Creates a builder whose output format, verbosity, and target environment follow the command-line flags.
    /// </summary>
    public static CliApplicationBuilder Create(ParseResult parseResult) =>
        new(ReporterFactory.ResolveFormat(parseResult), ReporterFactory.ResolveVerbosity(parseResult),
            allowRestore: !CommonOptions.NoInit.GetValueOrDefault(parseResult, false),
            environment: ConfigurationFactory.ResolveEnvironment(parseResult));
}
