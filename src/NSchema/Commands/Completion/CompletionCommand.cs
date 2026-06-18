using System.CommandLine;

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

        command.SetAction(parseResult => Console.Out.Write(CompletionScripts.For(parseResult.GetValue(shell)!)));

        return command;
    }
}
