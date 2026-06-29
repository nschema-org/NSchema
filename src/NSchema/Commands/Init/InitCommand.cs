using System.CommandLine;
using NSchema.Configuration;
using NSchema.Configuration.Plugins;

namespace NSchema.Commands.Init;

internal static class InitCommand
{
    public static Command Create()
    {
        var command = new Command("init", "Restore the provider and backend plugins pinned in the project configuration.");
        command.SetAction(Run);
        return command;
    }

    private static async Task Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var environment = ConfigurationFactory.ResolveEnvironment(parseResult);
        var configuration = await ConfigurationFactory.Load<InitConfiguration>(parseResult, environment, cancellationToken);

        using var app = CliApplicationBuilder.Create(parseResult).Build();

        // The file backend is built in, so only the provider and a plugin-backed state store need restoring.
        var references = new List<PluginReference>();
        if (configuration.Provider is { } provider)
        {
            references.Add(provider);
        }
        if (configuration.State?.Plugin is { } backend)
        {
            references.Add(backend);
        }

        if (references.Count == 0)
        {
            app.Messenger.Announce($"Nothing to restore: no provider or plugin backend is configured.");
            return;
        }

        var loader = new PluginLoader();
        foreach (var reference in references)
        {
            // Probe before loading so we can tell the user whether this run actually fetched the plugin or just found
            // it already cached. The "Restoring..." progress line is only worth showing for the slow path (a real
            // publish); a cache hit is instant. Either way we still Load, so an already-cached plugin is revalidated.
            var alreadyInstalled = loader.Cache.Contains(reference.PackageId, reference.Version);
            if (!alreadyInstalled)
            {
                app.Messenger.Announce($"Restoring {reference.PackageId} {reference.Version}...");
            }

            loader.Load(reference.PackageId, reference.Version).ThrowIfFailure();

            if (alreadyInstalled)
            {
                app.Messenger.Success($"{reference.PackageId} {reference.Version} (already installed)");
            }
            else
            {
                app.Messenger.Success($"{reference.PackageId} {reference.Version} (installed)");
            }
        }
    }
}
