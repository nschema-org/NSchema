using System.CommandLine;
using NSchema.Configuration.Model;
using NSchema.Configuration.Plugins;
using NSchema.Services.Reporting;

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

        var messenger = ReporterFactory.CreateMessenger(parseResult);

        // A malformed id or version can't match a cached entry (every cache dir is a valid id/version), so treat it
        // as a miss and fall through to the "nothing removed" reporting below rather than throwing.
        SemanticVersion? parsedVersion = null;
        var matchable = PackageId.IsValid(package) && (version is null || SemanticVersion.TryParse(version, out parsedVersion));

        IReadOnlyList<CachedPlugin> removed = [];
        if (matchable)
        {
            removed = new PluginCache().Remove(new PackageId(package), parsedVersion);
        }

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
