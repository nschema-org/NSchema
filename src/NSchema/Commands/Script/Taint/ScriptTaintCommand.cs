using System.CommandLine;
using NSchema.State;
using NSchema.State.Locks;

namespace NSchema.Commands.Script.Taint;

internal static class ScriptTaintCommand
{
    private static readonly Argument<string> NameArgument = new("name")
    {
        Description = "The declared name of the script whose recorded execution should be removed.",
    };

    public static Command Create()
    {
        var command = new Command("taint", "Remove a script's recorded execution from the state, so it runs again on the next apply.");

        command.Arguments.Add(NameArgument);
        command.Options.AddRange(ScriptTaintOptions.All);

        command.SetAction(Run);
        return command;
    }

    private static Task<int> Run(ParseResult parseResult, CancellationToken cancellationToken) => CommandRunner.Run(
        parseResult,
        configure: (builder, configuration) => builder.ConfigureState(configuration.State),
        command: Execute,
        validator: new ScriptTaintConfigurationValidator(),
        announceEnvironment: true,
        cancellationToken: cancellationToken
    );

    private static async Task<int> Execute(CommandContext<ScriptTaintConfiguration> context, CancellationToken cancellationToken)
    {
        var (app, configuration, parseResult, _) = context;

        var name = parseResult.GetValue(NameArgument)!;

        var locked = await app.Locks.Acquire(new AcquireLockArguments("script taint") { SkipLock = configuration.NoLock }, cancellationToken);
        if (locked.IsFailure)
        {
            app.Reporter.ReportDiagnostics(locked.Diagnostics);
            return ExitCodes.Error;
        }

        if (locked.Diagnostics.Count > 0)
        {
            app.Reporter.ReportDiagnostics(locked.Diagnostics);
        }

        try
        {
            return await TaintUnderLock(app, name, cancellationToken);
        }
        finally
        {
            await locked.Require().Release(CancellationToken.None);
        }
    }

    private static async Task<int> TaintUnderLock(CliApplication app, string name, CancellationToken cancellationToken)
    {
        // Read the state
        var read = await app.State.Read(new StateReadArguments(), cancellationToken);
        if (read.IsFailure)
        {
            app.Reporter.ReportDiagnostics(read.Diagnostics);
            return ExitCodes.Error;
        }

        if (read.Value?.State is not { } state || state.FindScript(name) is not { } execution)
        {
            app.Reporter.Warn($"No execution is recorded for script '{name}'; there is nothing to taint.");
            return ExitCodes.Error;
        }

        // Remove the script from the state's execution history.
        state = state.RemoveExecution(execution.Script);

        // Write the result.
        var written = await app.State.Write(new StateWriteArguments(state), cancellationToken);
        if (written.IsFailure)
        {
            app.Reporter.ReportDiagnostics(written.Diagnostics);
            return ExitCodes.Error;
        }

        app.Reporter.Success($"Removed the recorded execution for '{name}'. It will run again on the next apply.");
        return ExitCodes.NoChanges;
    }
}
