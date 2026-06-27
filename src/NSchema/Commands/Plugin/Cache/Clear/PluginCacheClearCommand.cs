using System.CommandLine;
using NSchema.Configuration.Plugins;
using NSchema.Services;

namespace NSchema.Commands.Plugin.Cache.Clear;

internal static class PluginCacheClearCommand
{
    public static Command Create()
    {
        var command = new Command("clear", "Remove every plugin from the shared cache. Each is re-restored on the next run (or 'nschema init').");
        command.SetAction(Run);
        return command;
    }

    // Project-independent, and safe to skip confirmation: the cache is just a restorable copy.
    private static Task Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var messenger = ConsoleMessenger.Create(parseResult);
        var cleared = new PluginCache().Clear();

        if (cleared.Count == 0)
        {
            messenger.Announce($"The plugin cache is already empty.");
            return Task.CompletedTask;
        }

        var noun = cleared.Count == 1 ? "plugin" : "plugins";
        messenger.Success($"Cleared {cleared.Count} {noun} from the cache.");
        return Task.CompletedTask;
    }
}
