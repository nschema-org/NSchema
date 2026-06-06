using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using NSchema.Cli.Configuration;
using NSchema.Resolution;
using NSchema.Schema.Serialization;
using Spectre.Console;

namespace NSchema.Cli.Commands.Init;

internal static class InitCommand
{
    public static Command Create()
    {
        var command = new Command("init", "Scaffold an nschema.json config and a sample schema in the current directory.");
        command.Options.Add(InitOptions.Format);
        command.Options.Add(InitOptions.Force);
        command.SetAction(Run);
        return command;
    }

    private static async Task Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var format = parseResult.GetValue(InitOptions.Format);
        var force = parseResult.GetValue(InitOptions.Force);

        using var app = CliApplicationBuilder.Create().Build();
        var serializers = app.Services.GetRequiredService<IKeyedResolver<ISchemaDocumentSerializer>>();
        var console = app.Services.GetRequiredService<IAnsiConsole>();

        var created = await new ProjectScaffolder()
            .Scaffold(Directory.GetCurrentDirectory(), format, force, serializers, cancellationToken);

        var tree = new Tree("[bold]Created[/]");
        foreach (var file in created)
        {
            tree.AddNode(Markup.FromInterpolated($"[green]✓[/] {file}"));
        }

        console.Write(tree);
        console.WriteLine();
        console.MarkupLineInterpolated($"Set [yellow]{EnvironmentVariables.ConnectionString}[/], then run [green]nschema plan[/].");
    }
}
