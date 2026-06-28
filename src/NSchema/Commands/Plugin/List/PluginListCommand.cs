using System.CommandLine;
using NSchema.Configuration;
using NSchema.Configuration.Plugins;
using NSchema.Services.Reporting;

namespace NSchema.Commands.Plugin.List;

internal static class PluginListCommand
{
    public static Command Create()
    {
        var command = new Command("list", "List the provider and backend plugins this project uses, and whether each is restored.");
        command.SetAction(Run);
        return command;
    }

    private static async Task Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var environment = ConfigurationFactory.ResolveEnvironment(parseResult);
        var configuration = await ConfigurationFactory.Load<PluginListConfiguration>(parseResult, environment, cancellationToken);

        var messenger = ConsoleMessenger.Create(parseResult);
        var plugins = PluginInventory.ForProject(configuration.Provider, configuration.State, new PluginCache());
        messenger.ReportProjectPlugins(plugins);
    }
}
