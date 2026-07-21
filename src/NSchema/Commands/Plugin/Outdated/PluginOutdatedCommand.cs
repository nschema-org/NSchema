using System.CommandLine;
using NSchema.Configuration;
using NSchema.Configuration.Plugins;
using NSchema.Services.Reporting;

namespace NSchema.Commands.Plugin.Outdated;

internal static class PluginOutdatedCommand
{
    public static Command Create()
    {
        var command = new Command("outdated", "Show, for each project plugin, its pinned version against the newest its range allows and the newest available.");
        command.SetAction(Run);
        return command;
    }

    private static async Task Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var environment = ConfigurationFactory.ResolveEnvironment(parseResult);
        ConfigurationFactory.ApplyWorkingDirectory(parseResult);
        var root = Directory.GetCurrentDirectory();

        var messenger = ReporterFactory.CreateMessenger(parseResult);

        var configuration = await ProjectConfigurationReader.Read(root, environment, cancellationToken);

        var outdated = Inspect(configuration, new PluginLoader());
        messenger.ReportOutdatedPlugins(outdated);
    }

    private static List<OutdatedPlugin> Inspect(ProjectConfiguration config, PluginLoader loader)
    {
        var plugins = new List<OutdatedPlugin>();

        if (config.Database is { } provider)
        {
            plugins.Add(Describe(PluginInventory.DatabaseRole, provider, config.Plugins, loader));
        }
        if (config.State?.Plugin is { } backend)
        {
            plugins.Add(Describe(PluginInventory.StateRole, backend, config.Plugins, loader));
        }

        return plugins;
    }

    private static OutdatedPlugin Describe(string role, PluginReference reference, IReadOnlyList<PluginDeclaration> declarations, PluginLoader loader)
    {
        var declaration = declarations.First(declaration => declaration.Label == reference.Label);

        // 'Wanted' is what 'plugin update' would install: the highest the range admits (an exact pin admits only itself).
        var wanted = declaration.Package.Version.IsExact
            ? reference.Version
            : loader.ResolveHighest(declaration.Package.Source, declaration.Package.Version);

        var latest = loader.ResolveLatestVersion(reference.PackageId);
        var outdated = reference.Version.CompareTo(latest) < 0;

        return new OutdatedPlugin(role, reference.Label, reference.PackageId, reference.Version, wanted, latest, outdated);
    }
}
