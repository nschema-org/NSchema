using System.CommandLine;
using NSchema.Configuration;
using NSchema.Configuration.Domain;
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

    private static async Task<int> Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var messenger = ReporterFactory.CreateMessenger(parseResult);
        var environment = ConfigurationFactory.ResolveEnvironment(parseResult);
        ConfigurationFactory.ApplyWorkingDirectory(parseResult);
        var root = Directory.GetCurrentDirectory();

        var configuration = await ProjectConfigurationReader.Read(root, environment, cancellationToken);
        if (configuration.ReportFailure(messenger))
        {
            return ExitCodes.Error;
        }

        var outdated = Inspect(configuration.Require(), new PluginLoader(root));
        if (outdated.ReportFailure(messenger))
        {
            return ExitCodes.Error;
        }

        messenger.ReportOutdatedPlugins(outdated.Require());
        return ExitCodes.NoChanges;
    }

    private static Result<List<OutdatedPlugin>> Inspect(ProjectConfiguration config, PluginLoader loader)
    {
        var plugins = new List<OutdatedPlugin>();

        if (config.Database is { } provider)
        {
            var described = Describe(PluginInventory.DatabaseRole, provider, config.Plugins, loader);
            if (described.IsFailure)
            {
                return Result.Failure<List<OutdatedPlugin>>(described.Diagnostics);
            }

            plugins.Add(described.Require());
        }

        if (config.State?.Plugin is { } backend)
        {
            var described = Describe(PluginInventory.StateRole, backend, config.Plugins, loader);
            if (described.IsFailure)
            {
                return Result.Failure<List<OutdatedPlugin>>(described.Diagnostics);
            }

            plugins.Add(described.Require());
        }

        return plugins;
    }

    private static Result<OutdatedPlugin> Describe(string role, PluginReference reference, IReadOnlyList<PluginDeclaration> declarations, PluginLoader loader)
    {
        var declaration = declarations.First(declaration => declaration.Label == reference.Label);

        // 'Wanted' is what 'plugin update' would install: the highest the range admits (an exact pin admits only itself).
        // An unsatisfiable range is the loader's own finding, so its diagnostics are carried rather than restated.
        SemanticVersion wanted;
        if (declaration.Package.Version.IsExact)
        {
            wanted = reference.Version;
        }
        else
        {
            var highest = loader.ResolveHighest(declaration.Package.Source, declaration.Package.Version);
            if (highest.IsFailure)
            {
                return Result.Failure<OutdatedPlugin>(highest.Diagnostics);
            }

            wanted = highest.Require();
        }

        var latest = loader.ResolveLatestVersion(reference.PackageId);
        if (latest.IsFailure)
        {
            return Result.Failure<OutdatedPlugin>(latest.Diagnostics);
        }

        var outdated = reference.Version.CompareTo(latest.Require()) < 0;

        return new OutdatedPlugin(role, reference.Label, reference.PackageId, reference.Version, wanted, latest.Require(), outdated);
    }
}
