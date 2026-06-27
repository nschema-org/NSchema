using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using NSchema.Services;

namespace NSchema.Commands.Completion;

internal static class CompletionUninstallCommand
{
    public static Command Create()
    {
        var shell = CompletionCommand.ShellArgument();

        var command = new Command("uninstall", "Remove the completion script from the shell's startup file.");
        command.Arguments.Add(shell);

        command.SetAction((parseResult, cancellationToken) => Run(parseResult, parseResult.GetValue(shell)!, cancellationToken));
        return command;
    }

    private static async Task Run(ParseResult parseResult, string shell, CancellationToken cancellationToken)
    {
        using var app = CliApplicationBuilder.Create(parseResult).Build();
        var presenter = app.Services.GetRequiredService<IConsolePresenter>();

        var outcome = await CompletionInstaller.Uninstall(shell, cancellationToken);
        if (outcome.Changed)
        {
            presenter.Success($"Removed {shell} completion from {outcome.Path}.");
        }
        else
        {
            presenter.Announce($"No {shell} completion found in {outcome.Path}.");
        }
    }
}
