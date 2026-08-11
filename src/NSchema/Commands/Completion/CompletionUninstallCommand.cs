using System.CommandLine;

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

    private static async Task<int> Run(ParseResult parseResult, string shell, CancellationToken cancellationToken)
    {
        // An unreadable .editorconfig fails the build, and requiring a value it does not carry would crash the
        // command with an internal error rather than saying what is wrong with the file.
        var built = CliApplicationBuilder.Create(parseResult).Build();
        if (built.ReportFailure(ReporterFactory.CreateReporter(parseResult)))
        {
            return ExitCodes.Error;
        }

        using var app = built.Require();

        var outcome = await CompletionInstaller.Uninstall(shell, cancellationToken);
        if (outcome.Changed)
        {
            app.Reporter.Success($"Removed {shell} completion from {outcome.Path}.");
        }
        else
        {
            app.Reporter.Announce($"No {shell} completion found in {outcome.Path}.");
        }

        return ExitCodes.NoChanges;
    }
}
