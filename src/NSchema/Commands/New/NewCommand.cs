using System.CommandLine;
using NSchema.Commands.Init;
using NSchema.Configuration;
using NSchema.Configuration.Domain;
using NSchema.Configuration.Plugins;
using NSchema.Plugins;
using NSchema.Services.Prompting;
using Spectre.Console;

namespace NSchema.Commands.New;

internal static class NewCommand
{
    public static Command Create()
    {
        var command = new Command("new", "Create a new project in the current directory.");
        command.Options.AddRange(NewOptions.All);
        command.SetAction(Run);
        return command;
    }

    private static async Task<int> Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        // An unreadable .editorconfig fails the build, and requiring a value it does not carry would crash the
        // command with an internal error rather than saying what is wrong with the file.
        var built = CliApplicationBuilder.Create(parseResult).Build();
        if (built.ReportFailure(ReporterFactory.CreateReporter(parseResult)))
        {
            return ExitCodes.Error;
        }

        using var app = built.Require();
        var console = AnsiConsole.Console;

        var resolved = await ConfigurationFactory.Load<NewConfiguration>(parseResult, environment: null, cancellationToken);
        if (resolved.ReportFailure(app.Reporter))
        {
            return ExitCodes.Error;
        }

        var configuration = resolved.Require();
        var loader = new PluginLoader(Directory.GetCurrentDirectory());

        // The database plugin renders its own DATABASE statement and supplies a dialect-specific sample schema; the
        // CLI authors the PLUGIN declaration, since it resolved the package and version. Resolve the latest version
        // compatible with this CLI and pin it.
        var plugins = new List<ResolvedPlugin>();
        var (providerPackageName, providerLabel) = DatabasePackage(configuration.Database);
        var providerPackage = new PackageId(providerPackageName);
        app.Reporter.Announce($"Resolving {providerPackage}...");

        var providerResolution = loader.ResolveLatestVersion(providerPackage);
        if (providerResolution.ReportFailure(app.Reporter))
        {
            return ExitCodes.Error;
        }

        var providerVersion = providerResolution.Require();
        plugins.Add(new ResolvedPlugin(new PluginLabel(providerLabel), providerPackage, providerVersion));
        var resolvedProvider = Resolve<INSchemaDatabasePlugin>(loader, providerPackage, providerVersion);
        if (resolvedProvider.ReportFailure(app.Reporter))
        {
            return ExitCodes.Error;
        }

        var providerPlugin = resolvedProvider.Require();
        var configured = Configure(console, providerPlugin, new ScaffoldContext(), configuration.Answers);
        if (configured.ReportFailure(app.Reporter))
        {
            return ExitCodes.Error;
        }

        var providerContext = configured.Require();
        var databaseConfiguration = providerPlugin.GetScaffoldTemplate(providerContext);

        // Every plugin is asked for the environment overlay as well as the base, reusing the same answers; one with
        // nothing that differs per environment says so by contributing no statements.
        var databaseOverlay = providerPlugin.GetScaffoldTemplate(providerContext with { EnvironmentName = "prod" });
        var sampleSchema = providerPlugin.GetSampleSchema();

        // The local-file state store is built in; any other backend is a plugin that renders its own statements
        // (base + overlay).
        var state = ProjectScaffolder.FileState;
        var stateOverlay = ProjectScaffolder.FileStateOverlay;
        if (StatePackage(configuration.State) is { } backend)
        {
            var backendPackage = new PackageId(backend.Package);
            app.Reporter.Announce($"Resolving {backendPackage}...");

            var backendResolution = loader.ResolveLatestVersion(backendPackage);
            if (backendResolution.ReportFailure(app.Reporter))
            {
                return ExitCodes.Error;
            }

            var backendVersion = backendResolution.Require();
            plugins.Add(new ResolvedPlugin(new PluginLabel(backend.Label), backendPackage, backendVersion));
            var resolvedBackend = Resolve<INSchemaStatePlugin>(loader, backendPackage, backendVersion);
            if (resolvedBackend.ReportFailure(app.Reporter))
            {
                return ExitCodes.Error;
            }

            var backendPlugin = resolvedBackend.Require();
            var backendConfigured = Configure(console, backendPlugin, new ScaffoldContext(), configuration.Answers);
            if (backendConfigured.ReportFailure(app.Reporter))
            {
                return ExitCodes.Error;
            }

            var backendContext = backendConfigured.Require();

            // The questions are put once; the overlay reuses those answers, varying only by environment.
            state = backendPlugin.GetScaffoldTemplate(backendContext);
            stateOverlay = backendPlugin.GetScaffoldTemplate(backendContext with { EnvironmentName = "prod" });
        }

        var scaffolded = await ProjectScaffolder.Scaffold(
            Directory.GetCurrentDirectory(),
            configuration.Force,
            new ProjectTemplate
            {
                EngineRequirement = EngineRequirement(),
                Plugins = plugins,
                Database = databaseConfiguration,
                DatabaseOverlay = databaseOverlay,
                State = state,
                StateOverlay = stateOverlay,
                Schema = sampleSchema,
            },
            cancellationToken);

        if (scaffolded.ReportFailure(app.Reporter))
        {
            return ExitCodes.Error;
        }

        var tree = new Tree("[bold]Created[/]");
        foreach (var file in scaffolded.Require())
        {
            tree.AddNode(Markup.FromInterpolated($"[green]✓[/] {file}"));
        }

        console.Write(tree);
        console.WriteLine();

        // Leave a ready-to-run project: resolve and lock the plugins just declared (they are already restored above).
        // '--no-init' opts out for an offline or edit-first workflow.
        if (!configuration.NoInit)
        {
            var initialized = await ProjectInitializer.Initialize(Directory.GetCurrentDirectory(), environment: null, loader, app.Reporter, cancellationToken);
            if (initialized.ReportFailure(app.Reporter))
            {
                return ExitCodes.Error;
            }
        }

        // SQLite's connection string (a local file path) is already filled in; the others need a secret supplied out of
        // band, so point the user at the right environment variable.
        if (configuration.Database == DatabaseKind.Sqlite)
        {
            app.Reporter.Announce($"Edit {"connection_string"} in {"config.sql"}, then run {"nschema plan"}.");
        }
        else
        {
            app.Reporter.Announce($"Set {EnvironmentVariables.DatabaseConnectionString}, then run {"nschema plan"}.");
        }

        return ExitCodes.NoChanges;
    }

    /// <summary>
    /// Puts the plugin's questions and returns the context carrying the answers. A plugin that declares none is
    /// unaffected, so this is silent for anything that scaffolds fixed placeholders.
    /// </summary>
    internal static Result<ScaffoldContext> Configure(
        IAnsiConsole console,
        INSchemaPlugin plugin,
        ScaffoldContext context,
        IReadOnlyDictionary<string, string?> supplied
    )
    {
        var prompts = plugin.GetScaffoldPrompts(context);
        if (prompts.Count == 0)
        {
            return context;
        }

        return ScaffoldPrompter.Answer(console, prompts, supplied)
            .Map(answers => context with { Answers = answers });
    }

    // The engine is compiled into the CLI, so a project scaffolded now requires this CLI's engine major: [X.0, X+1.0).
    private static string EngineRequirement()
    {
        var major = HostVersion.Current.Major;
        return $"[{major}.0,{major + 1}.0)";
    }

    // A plugin is resolved by capability: the package supplies at most one plugin per capability interface. A package
    // that supplies neither is a configuration problem, not a bug — CliApplicationBuilder reports the same condition
    // as a diagnostic, so this matches.
    private static Result<TPlugin> Resolve<TPlugin>(PluginLoader loader, PackageId packageId, SemanticVersion version)
        where TPlugin : class, INSchemaPlugin
    {
        var loaded = loader.Load(packageId, version);
        if (loaded.IsFailure)
        {
            return Result.Failure<TPlugin>(loaded.Diagnostics);
        }

        var plugin = loaded.Require().OfType<TPlugin>().FirstOrDefault();
        if (plugin is not null)
        {
            return plugin;
        }

        return PluginDiagnostics.MissingCapability(packageId);
    }

    private static (string Package, string Label) DatabasePackage(DatabaseKind database) => database switch
    {
        DatabaseKind.Postgres => ("NSchema.Postgres", "postgres"),
        DatabaseKind.Sqlite => ("NSchema.Sqlite", "sqlite"),
        DatabaseKind.SqlServer => ("NSchema.SqlServer", "sqlserver"),
        _ => throw new ArgumentOutOfRangeException(nameof(database), database, "Unknown provider."),
    };

    // The file state store is built into the core, so it maps to no package; every other backend is a plugin.
    private static (string Package, string Label)? StatePackage(StateKind state) => state switch
    {
        StateKind.File => null,
        StateKind.S3 => ("NSchema.Aws", "s3"),
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown backend."),
    };
}
