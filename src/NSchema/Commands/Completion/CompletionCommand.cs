using System.CommandLine;
using Spectre.Console;

namespace NSchema.Commands.Completion;

internal static class CompletionCommand
{
    internal static readonly string[] Shells = ["bash", "zsh", "fish", "pwsh"];

    public static Command Create()
    {
        var shell = new Argument<string>("shell")
        {
            Description = $"The shell to generate a completion script for ({string.Join(", ", Shells)}).",
        };
        shell.AcceptOnlyFromAmong(Shells);

        var command = new Command("completion", "Output a shell tab-completion script for nschema.");
        command.Arguments.Add(shell);
        command.Options.AddRange(CompletionOptions.All);

        command.SetAction((parseResult, cancellationToken) => Run(
            parseResult.GetValue(shell)!,
            parseResult.GetValue(CompletionOptions.Install.Option),
            parseResult.GetValue(CompletionOptions.Uninstall.Option),
            cancellationToken
        ));

        return command;
    }

    private static async Task Run(string shell, bool install, bool uninstall, CancellationToken cancellationToken)
    {
        if (install && uninstall)
        {
            throw new InvalidOperationException("Specify only one of --install-autocomplete or --uninstall-autocomplete.");
        }

        if (install)
        {
            var outcome = await CompletionInstaller.Install(shell, cancellationToken);
            if (outcome.Changed)
            {
                AnsiConsole.MarkupLineInterpolated($"[green]✓[/] Installed {shell} completion in [yellow]{outcome.Path}[/]. Restart your shell to enable it.");
            }
            else
            {
                AnsiConsole.MarkupLineInterpolated($"{shell} completion is already installed in [yellow]{outcome.Path}[/].");
            }

            return;
        }

        if (uninstall)
        {
            var outcome = await CompletionInstaller.Uninstall(shell, cancellationToken);
            if (outcome.Changed)
            {
                AnsiConsole.MarkupLineInterpolated($"[green]✓[/] Removed {shell} completion from [yellow]{outcome.Path}[/].");
            }
            else
            {
                AnsiConsole.MarkupLineInterpolated($"No {shell} completion found in [yellow]{outcome.Path}[/].");
            }

            return;
        }

        Console.Out.Write(CompletionScripts.For(shell));
    }
}
