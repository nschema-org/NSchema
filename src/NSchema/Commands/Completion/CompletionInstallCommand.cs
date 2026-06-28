using System.CommandLine;
using NSchema.Services;

namespace NSchema.Commands.Completion;

internal static class CompletionInstallCommand
{
    public static Command Create()
    {
        var shell = CompletionCommand.ShellArgument();

        var command = new Command("install", "Install the completion script into the shell's startup file.");
        command.Arguments.Add(shell);

        command.SetAction((parseResult, cancellationToken) => Run(parseResult, parseResult.GetValue(shell)!, cancellationToken));
        return command;
    }

    private static async Task Run(ParseResult parseResult, string shell, CancellationToken cancellationToken)
    {
        using var app = CliApplicationBuilder.Create(parseResult).Build();

        var outcome = await CompletionInstaller.Install(shell, cancellationToken);
        if (outcome.Changed)
        {
            app.Messenger.Success($"Installed {shell} completion in {outcome.Path}. Restart your shell to enable it.");
        }
        else
        {
            app.Messenger.Announce($"{shell} completion is already installed in {outcome.Path}.");
        }
    }
}
