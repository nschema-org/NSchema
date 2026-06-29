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

        // The provider plugin renders its own config block and supplies a dialect-specific sample schema. Resolve the
        // latest version compatible with this CLI and pin it.
        var (providerPackage, providerLabel) = ProviderPackage(configuration.Provider);
        app.Messenger.Announce($"Resolving {providerPackage}...");
        var providerVersion = loader.ResolveLatestVersion(providerPackage);
        var providerPlugin = Resolve<INSchemaProviderPlugin>(loader, providerPackage, providerLabel, providerVersion);
        var providerBlock = providerPlugin.GetScaffoldTemplate(new ScaffoldContext { Version = providerVersion });
        var sampleSchema = providerPlugin.GetSampleSchema();

        // The local-file backend is built in; any other backend is a plugin that renders its own block (base + overlay).
        (string Base, string Overlay)? pluginBackend = null;
        if (BackendPackage(configuration.Backend) is { } backend)
        {
            app.Messenger.Announce($"Resolving {backend.Package}...");
            var backendVersion = loader.ResolveLatestVersion(backend.Package);
            var backendPlugin = Resolve<INSchemaBackendPlugin>(loader, backend.Package, backend.Label, backendVersion);
            pluginBackend = (
                backendPlugin.GetScaffoldTemplate(new ScaffoldContext { Version = backendVersion }),
                backendPlugin.GetScaffoldTemplate(new ScaffoldContext { Version = backendVersion, EnvironmentName = "prod" }));
        }

        var created = await ProjectScaffolder.Scaffold(
            Directory.GetCurrentDirectory(),
            configuration.Force,
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
            app.Messenger.Announce($"Edit {"connection_string"} in {"config.sql"}, then run {"nschema plan"}.");
        }
        else
        {
            app.Messenger.Announce($"Set {ConnectionStringEnvVar(configuration.Provider)}, then run {"nschema plan"}.");
        }
    }

    private static TPlugin Resolve<TPlugin>(PluginLoader loader, string packageId, string label, string version)
        where TPlugin : class, INSchemaPlugin =>
        loader.Load(packageId, version).ValueOrThrow()
            .OfType<TPlugin>()
            .FirstOrDefault(p => string.Equals(p.Label, label, StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException($"The package '{packageId}' does not provide a plugin for '{label}'.");

    private static (string Package, string Label) ProviderPackage(ProviderKind provider) => provider switch
    {
        ProviderKind.Postgres => ("NSchema.Postgres", "postgres"),
        ProviderKind.Sqlite => ("NSchema.Sqlite", "sqlite"),
        ProviderKind.SqlServer => ("NSchema.SqlServer", "sqlserver"),
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unknown provider."),
    };

    // The file backend is built into the core, so it maps to no package; every other backend is a plugin.
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
