using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using NSchema.Configuration;
using NSchema.Configuration.Plugins;
using Spectre.Console;

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

        using var app = CliApplicationBuilder.Create().Build();
        var console = app.Services.GetRequiredService<IAnsiConsole>();

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
            console.MarkupLine("Nothing to restore: no provider or plugin backend is configured.");
            return;
        }

        var loader = new PluginLoader();
        foreach (var reference in references)
        {
            console.MarkupLineInterpolated($"Restoring [yellow]{reference.PackageId}[/] {reference.Version}...");
            loader.Load(reference.PackageId, reference.Version);
            console.MarkupLineInterpolated($"[green]✓[/] {reference.PackageId} {reference.Version}");
        }
    }
}
