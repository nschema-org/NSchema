using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using NSchema.Configuration;
using Spectre.Console;

namespace NSchema.Commands.Init;

internal static class InitCommand
{
    public static Command Create()
    {
        var command = new Command("init", "Scaffold a simple project in the current directory.");
        command.Options.AddRange(InitOptions.All);
        command.SetAction(Run);
        return command;
    }

    private static async Task Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var configuration = await ConfigurationFactory.Load<InitConfiguration>(parseResult, cancellationToken);

        using var app = CliApplicationBuilder.Create().Build();
        var console = app.Services.GetRequiredService<IAnsiConsole>();

        var created = await ProjectScaffolder.Scaffold(Directory.GetCurrentDirectory(), configuration.Force, cancellationToken);

        var tree = new Tree("[bold]Created[/]");
        foreach (var file in created)
        {
            tree.AddNode(Markup.FromInterpolated($"[green]✓[/] {file}"));
        }

        console.Write(tree);
        console.WriteLine();
        console.MarkupLineInterpolated($"Set [yellow]{EnvironmentVariables.PostgresConnectionString}[/], then run [green]nschema plan[/].");
    }
}
