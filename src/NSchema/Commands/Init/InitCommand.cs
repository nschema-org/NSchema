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
        var configuration = await ConfigurationFactory.Load<InitConfiguration>(parseResult, environment: null, cancellationToken);

        using var app = CliApplicationBuilder.Create().Build();
        var console = app.Services.GetRequiredService<IAnsiConsole>();

        var created = await ProjectScaffolder.Scaffold(
            Directory.GetCurrentDirectory(),
            configuration.Force,
            configuration.Provider,
            configuration.Backend,
            cancellationToken
        );

        var tree = new Tree("[bold]Created[/]");
        foreach (var file in created)
        {
            tree.AddNode(Markup.FromInterpolated($"[green]✓[/] {file}"));
        }

        console.Write(tree);
        console.WriteLine();

        // SQLite's connection string (a local file path) is already filled in; the others need a secret supplied out of
        // band, so point the user at the right environment variable.
        if (configuration.Provider == ProviderKind.Sqlite)
        {
            console.MarkupLineInterpolated($"Edit [yellow]connection_string[/] in [yellow]config.sql[/], then run [green]nschema plan[/].");
        }
        else
        {
            console.MarkupLineInterpolated($"Set [yellow]{ConnectionStringEnvVar(configuration.Provider)}[/], then run [green]nschema plan[/].");
        }
    }

    private static string ConnectionStringEnvVar(ProviderKind provider) => provider switch
    {
        ProviderKind.Postgres => EnvironmentVariables.PostgresConnectionString,
        ProviderKind.SqlServer => EnvironmentVariables.SqlServerConnectionString,
        ProviderKind.Sqlite => EnvironmentVariables.SqliteConnectionString,
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unknown provider."),
    };
}
