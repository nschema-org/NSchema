using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using NSchema.Configuration;
using NSchema.Resolution;
using NSchema.Schema.Serialization;
using Spectre.Console;

namespace NSchema.Commands.Init;

internal static class InitCommand
{
    public static Command Create()
    {
        var command = new Command("init", "Scaffold an nschema.json config and a sample schema in the current directory.");
        command.Options.AddRange(InitOptions.All);
        command.SetAction(Run);
        return command;
    }

    private static async Task Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var configuration = ConfigurationFactory.Load<InitConfiguration>(parseResult);

        using var app = CliApplicationBuilder.Create().Build();
        var serializers = app.Services.GetRequiredService<IKeyedResolver<ISchemaSerializer>>();
        var console = app.Services.GetRequiredService<IAnsiConsole>();

        var created = await ProjectScaffolder.Scaffold(Directory.GetCurrentDirectory(), configuration.Format, configuration.Force, serializers, cancellationToken);

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
