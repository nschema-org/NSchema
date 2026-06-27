using System.CommandLine;
using NSchema.Commands.Plugin.Cache.Clear;
using NSchema.Commands.Plugin.Cache.List;
using NSchema.Commands.Plugin.Cache.Remove;

namespace NSchema.Commands.Plugin.Cache;

/// <summary>
/// The <c>plugin cache</c> command group.
/// </summary>
internal static class PluginCacheCommand
{
    public static Command Create()
    {
        var command = new Command("cache", "Inspect and remove entries in the shared plugin cache (~/.nschema/plugins).");

        command.Subcommands.Add(PluginCacheListCommand.Create());
        command.Subcommands.Add(PluginCacheRemoveCommand.Create());
        command.Subcommands.Add(PluginCacheClearCommand.Create());

        return command;
    }
}
