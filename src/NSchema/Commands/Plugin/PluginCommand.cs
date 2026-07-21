using System.CommandLine;
using NSchema.Commands.Plugin.Cache;
using NSchema.Commands.Plugin.List;
using NSchema.Commands.Plugin.Outdated;
using NSchema.Commands.Plugin.Show;
using NSchema.Commands.Plugin.Update;

namespace NSchema.Commands.Plugin;

/// <summary>
/// The <c>plugin</c> command group: inspect the provider/backend plugins a project uses and manage the shared cache.
/// </summary>
internal static class PluginCommand
{
    public static Command Create()
    {
        var command = new Command("plugin", "Inspect the project's provider and backend plugins, and manage the plugin cache.");

        command.Subcommands.Add(PluginListCommand.Create());
        command.Subcommands.Add(PluginShowCommand.Create());
        command.Subcommands.Add(PluginUpdateCommand.Create());
        command.Subcommands.Add(PluginOutdatedCommand.Create());
        command.Subcommands.Add(PluginCacheCommand.Create());

        return command;
    }
}
