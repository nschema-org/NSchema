using System.CommandLine;
using NSchema.Configuration.Plugins;
using NSchema.Services.Reporting;

namespace NSchema.Commands.Plugin.Cache.List;

internal static class PluginCacheListCommand
{
    public static Command Create()
    {
        var command = new Command("list", "List the plugin packages restored in the shared cache, across all projects.");
        command.SetAction(Run);
        return command;
    }

    // Project-independent: the cache is profile-level, so this reads no project config and contacts no infrastructure.
    private static Task Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var cache = new PluginCache();
        ReporterFactory.CreateMessenger(parseResult).ReportCachedPlugins(cache.Root, cache.List());
        return Task.CompletedTask;
    }
}
