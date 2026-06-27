using System.CommandLine;

namespace NSchema.Commands.Completion;

internal static class CompletionCommand
{
    internal static readonly string[] Shells = ["bash", "zsh", "fish", "pwsh"];

    /// <summary>
    /// Builds the <c>shell</c> positional accepted by <c>completion</c> and its subcommands.
    /// </summary>
    internal static Argument<string> ShellArgument()
    {
        var shell = new Argument<string>("shell")
        {
            Description = $"The shell to target ({string.Join(", ", Shells)}).",
        };
        shell.AcceptOnlyFromAmong(Shells);
        return shell;
    }

    public static Command Create()
    {
        var shell = ShellArgument();

        // `completion <shell>` emits the script (the default action); `completion install/uninstall <shell>` manage it.
        // The subcommand names never collide with the shell names, so the positional stays unambiguous.
        var command = new Command("completion", "Output a shell tab-completion script for nschema, or install/uninstall it.");
        command.Arguments.Add(shell);
        command.Subcommands.Add(CompletionInstallCommand.Create());
        command.Subcommands.Add(CompletionUninstallCommand.Create());

        command.SetAction((parseResult, _) =>
        {
            Console.Out.Write(CompletionScripts.For(parseResult.GetValue(shell)!));
            return Task.CompletedTask;
        });

        return command;
    }
}
