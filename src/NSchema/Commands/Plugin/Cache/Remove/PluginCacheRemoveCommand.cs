using System.CommandLine;
using NSchema.Configuration.Plugins;
using NSchema.Services;

namespace NSchema.Commands.Plugin.Cache.Remove;

internal static class PluginCacheRemoveCommand
{
    internal static readonly Argument<string> PackageArgument = new("package")
    {
        Description = "The plugin package id to remove from the cache (e.g. NSchema.Postgres).",
    };

    internal static readonly Argument<string?> VersionArgument = new("version")
    {
        Description = "The specific version to remove. When omitted, every cached version of the package is removed.",
        Arity = ArgumentArity.ZeroOrOne,
    };

    public static Command Create()
    {
        var command = new Command("remove", "Remove a plugin package from the shared cache, by package id and optional version.");
        command.Arguments.Add(PackageArgument);
        command.Arguments.Add(VersionArgument);
        command.SetAction(Run);
        return command;
    }

    // Project-independent, and safe to skip confirmation: the cache is just a restorable copy — anything removed is
    // re-fetched on the next run (or 'nschema init').
    private static Task Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var package = parseResult.GetValue(PackageArgument)!;
        var version = parseResult.GetValue(VersionArgument);

        var messenger = ConsoleMessenger.Create(parseResult);
        var removed = new PluginCache().Remove(package, version);

        if (removed.Count == 0)
        {
            if (version is null)
            {
                messenger.Warn($"No cached versions of '{package}' to remove.");
            }
            else
            {
                messenger.Warn($"'{package}' {version} is not in the cache.");
            }

            return Task.CompletedTask;
        }

        foreach (var entry in removed)
        {
            messenger.Success($"Removed {entry.PackageId} {entry.Version} from the cache.");
        }

        return Task.CompletedTask;
    }
}
