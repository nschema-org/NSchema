using System.CommandLine;
using NSchema.Configuration;
using NSchema.Configuration.Plugins;

namespace NSchema.Commands.Init;

internal static class InitCommand
{
    public static Command Create()
    {
        var command = new Command("init", "Restore the provider and backend plugins pinned in the project configuration, locking declared ranges.");
        command.SetAction(Run);
        return command;
    }

    private static async Task Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var environment = ConfigurationFactory.ResolveEnvironment(parseResult);
        ConfigurationFactory.ApplyWorkingDirectory(parseResult);
        var root = Directory.GetCurrentDirectory();

        using var app = CliApplicationBuilder.Create(parseResult).Build();

        await ProjectInitializer.Initialize(root, environment, new PluginLoader(), app.Messenger, cancellationToken);
    }
}
