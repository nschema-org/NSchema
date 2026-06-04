using System.CommandLine;
using NSchema.Cli.Configuration;

namespace NSchema.Cli.Commands.Init;

internal static class InitCommand
{
    public static Command Create()
    {
        var command = new Command("init", "Scaffold an nschema.json config and a sample schema in the current directory.");
        command.Options.Add(CliOptions.Init.Format);
        command.Options.Add(CliOptions.Init.Force);
        command.SetAction(Init);
        return command;
    }

    private static Task Init(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var format = parseResult.GetValue(CliOptions.Init.Format);
        var force = parseResult.GetValue(CliOptions.Init.Force);

        var created = new ProjectScaffolder().Scaffold(Directory.GetCurrentDirectory(), format, force);

        foreach (var file in created)
        {
            Console.WriteLine($"Created {file}");
        }

        Console.WriteLine();
        Console.WriteLine($"Set {EnvironmentVariables.ConnectionString}, then run `nschema plan`.");
        return Task.CompletedTask;
    }
}
