using System.CommandLine;
using NSchema.Commands.Init;
using NSchema.Configuration;
using NSchema.Configuration.Model;
using NSchema.Configuration.Plugins;
using NSchema.Plugins;
using Spectre.Console;

namespace NSchema.Commands.Scaffold;

internal static class ScaffoldCommand
{
    public static Command Create()
    {
        var command = new Command("scaffold", "Scaffold a simple project in the current directory.");
        command.Options.AddRange(ScaffoldOptions.All);
        command.SetAction(Run);
        return command;
    }

    private static async Task Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var configuration = await ConfigurationFactory.Load<ScaffoldConfiguration>(parseResult, environment: null, cancellationToken);

        using var app = CliApplicationBuilder.Create(parseResult).Build();
        var console = AnsiConsole.Console;

        var loader = new PluginLoader();

        // The database plugin renders its own DATABASE statement and supplies a dialect-specific sample schema; the
        // CLI authors the PLUGIN declaration, since it resolved the package and version. Resolve the latest version
        // compatible with this CLI and pin it.
        var plugins = new List<(string Label, string PackageId, string Version)>();
        var (providerPackageName, providerLabel) = DatabasePackage(configuration.Database);
        var providerPackage = new PackageId(providerPackageName);
        app.Messenger.Announce($"Resolving {providerPackage}...");
        var providerVersion = loader.ResolveLatestVersion(providerPackage);
        plugins.Add((providerLabel, providerPackage.Value, providerVersion.ToString()));
        var providerPlugin = Resolve<INSchemaDatabasePlugin>(loader, providerPackage, providerVersion);
        var providerBlock = providerPlugin.GetScaffoldTemplate(new ScaffoldContext());
        var sampleSchema = providerPlugin.GetSampleSchema();

        // The local-file state store is built in; any other backend is a plugin that renders its own statements
        // (base + overlay).
        (string Base, string Overlay)? pluginBackend = null;
        if (StatePackage(configuration.State) is { } backend)
        {
            var backendPackage = new PackageId(backend.Package);
            app.Messenger.Announce($"Resolving {backendPackage}...");
            var backendVersion = loader.ResolveLatestVersion(backendPackage);
            plugins.Add((backend.Label, backendPackage.Value, backendVersion.ToString()));
            var backendPlugin = Resolve<INSchemaStatePlugin>(loader, backendPackage, backendVersion);
            pluginBackend = (
                backendPlugin.GetScaffoldTemplate(new ScaffoldContext()),
                backendPlugin.GetScaffoldTemplate(new ScaffoldContext { EnvironmentName = "prod" }));
        }

        var created = await ProjectScaffolder.Scaffold(
            Directory.GetCurrentDirectory(),
            configuration.Force,
            EngineRequirement(),
            plugins,
            providerBlock,
            sampleSchema,
            pluginBackend,
            cancellationToken);

        var tree = new Tree("[bold]Created[/]");
        foreach (var file in created)
        {
            tree.AddNode(Markup.FromInterpolated($"[green]✓[/] {file}"));
        }

        console.Write(tree);
        console.WriteLine();

        // Leave a ready-to-run project: resolve and lock the plugins just declared (they are already restored above).
        // '--no-init' opts out for an offline or edit-first workflow.
        if (!configuration.NoInit)
        {
            await ProjectInitializer.Initialize(Directory.GetCurrentDirectory(), environment: null, loader, app.Messenger, cancellationToken);
        }

        // SQLite's connection string (a local file path) is already filled in; the others need a secret supplied out of
        // band, so point the user at the right environment variable.
        if (configuration.Database == DatabaseKind.Sqlite)
        {
            app.Messenger.Announce($"Edit {"connection_string"} in {"config.env.sql"}, then run {"nschema plan"}.");
        }
        else
        {
            app.Messenger.Announce($"Set {ConnectionStringEnvVar(configuration.Database)}, then run {"nschema plan"}.");
        }
    }

    // The engine is compiled into the CLI, so a project scaffolded now requires this CLI's engine major: [X.0, X+1.0).
    private static string EngineRequirement()
    {
        var major = HostVersion.Current.Major;
        return $"[{major}.0,{major + 1}.0)";
    }

    // A plugin is resolved by capability: the package supplies at most one plugin per capability interface.
    private static TPlugin Resolve<TPlugin>(PluginLoader loader, PackageId packageId, SemanticVersion version)
        where TPlugin : class, INSchemaPlugin =>
        loader.Load(packageId, version).Require().OfType<TPlugin>().FirstOrDefault()
        ?? throw new InvalidOperationException($"The package '{packageId}' does not provide the expected plugin capability.");

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

    private static string ConnectionStringEnvVar(DatabaseKind database) => database switch
    {
        DatabaseKind.Postgres => EnvironmentVariables.PostgresConnectionString,
        DatabaseKind.SqlServer => EnvironmentVariables.SqlServerConnectionString,
        DatabaseKind.Sqlite => EnvironmentVariables.SqliteConnectionString,
        _ => throw new ArgumentOutOfRangeException(nameof(database), database, "Unknown provider."),
    };
}
