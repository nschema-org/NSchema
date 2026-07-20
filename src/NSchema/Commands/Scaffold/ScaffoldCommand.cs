using System.CommandLine;
using NSchema.Configuration;
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
        var (providerPackage, providerLabel) = ProviderPackage(configuration.Provider);
        app.Messenger.Announce($"Resolving {providerPackage}...");
        var providerVersion = loader.ResolveLatestVersion(providerPackage);
        plugins.Add((providerLabel, providerPackage, providerVersion));
        var providerPlugin = Resolve<INSchemaDatabasePlugin>(loader, providerPackage, providerVersion);
        var providerBlock = providerPlugin.GetScaffoldTemplate(new ScaffoldContext());
        var sampleSchema = providerPlugin.GetSampleSchema();

        // The local-file state store is built in; any other backend is a plugin that renders its own statements
        // (base + overlay).
        (string Base, string Overlay)? pluginBackend = null;
        if (BackendPackage(configuration.Backend) is { } backend)
        {
            app.Messenger.Announce($"Resolving {backend.Package}...");
            var backendVersion = loader.ResolveLatestVersion(backend.Package);
            plugins.Add((backend.Label, backend.Package, backendVersion));
            var backendPlugin = Resolve<INSchemaStatePlugin>(loader, backend.Package, backendVersion);
            pluginBackend = (
                backendPlugin.GetScaffoldTemplate(new ScaffoldContext()),
                backendPlugin.GetScaffoldTemplate(new ScaffoldContext { EnvironmentName = "prod" }));
        }

        var created = await ProjectScaffolder.Scaffold(
            Directory.GetCurrentDirectory(),
            configuration.Force,
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

        // SQLite's connection string (a local file path) is already filled in; the others need a secret supplied out of
        // band, so point the user at the right environment variable.
        if (configuration.Provider == ProviderKind.Sqlite)
        {
            app.Messenger.Announce($"Edit {"connection_string"} in {"config.env.sql"}, then run {"nschema plan"}.");
        }
        else
        {
            app.Messenger.Announce($"Set {ConnectionStringEnvVar(configuration.Provider)}, then run {"nschema plan"}.");
        }
    }

    // A plugin is resolved by capability: the package supplies at most one plugin per capability interface.
    private static TPlugin Resolve<TPlugin>(PluginLoader loader, string packageId, string version)
        where TPlugin : class, INSchemaPlugin =>
        loader.Load(packageId, version).ValueOrThrow().OfType<TPlugin>().FirstOrDefault()
        ?? throw new InvalidOperationException($"The package '{packageId}' does not provide the expected plugin capability.");

    private static (string Package, string Label) ProviderPackage(ProviderKind provider) => provider switch
    {
        ProviderKind.Postgres => ("NSchema.Postgres", "postgres"),
        ProviderKind.Sqlite => ("NSchema.Sqlite", "sqlite"),
        ProviderKind.SqlServer => ("NSchema.SqlServer", "sqlserver"),
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unknown provider."),
    };

    // The file state store is built into the core, so it maps to no package; every other backend is a plugin.
    private static (string Package, string Label)? BackendPackage(BackendKind backend) => backend switch
    {
        BackendKind.File => null,
        BackendKind.S3 => ("NSchema.Aws", "s3"),
        _ => throw new ArgumentOutOfRangeException(nameof(backend), backend, "Unknown backend."),
    };

    private static string ConnectionStringEnvVar(ProviderKind provider) => provider switch
    {
        ProviderKind.Postgres => EnvironmentVariables.PostgresConnectionString,
        ProviderKind.SqlServer => EnvironmentVariables.SqlServerConnectionString,
        ProviderKind.Sqlite => EnvironmentVariables.SqliteConnectionString,
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unknown provider."),
    };
}
