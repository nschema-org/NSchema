using System.CommandLine;
using NSchema.Configuration;
using NSchema.Policies;
using NSchema.State;
using NSchema.State.Storage;

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

    private static async ValueTask<ScriptTaintConfiguration> Resolve(ParseResult result, string? environment, CancellationToken cancellationToken)
    {
        var config = await ConfigurationFactory.Load<ScriptTaintConfiguration>(result, environment, cancellationToken);
        new ScriptTaintConfigurationValidator().ValidateOrThrow(config);
        return config;
    }

    private static async Task<int> Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var environment = ConfigurationFactory.ResolveEnvironment(parseResult);
        var configuration = await Resolve(parseResult, environment, cancellationToken);
        var name = parseResult.GetValue(NameArgument)!;

        using var app = CliApplicationBuilder.Create(parseResult)
            .ConfigureBackendState(configuration.State)
            .Build();
        app.Messenger.ReportEnvironment(environment);

        var locked = await app.Locks.Acquire("script taint", configuration.NoLock, cancellationToken);
        if (locked.IsFailure)
        {
            app.Messenger.ReportDiagnostics(new PolicyDiagnostics([.. locked.Diagnostics]));
            return ExitCodes.Error;
        }

        if (locked.Diagnostics.Count > 0)
        {
            app.Messenger.ReportDiagnostics(new PolicyDiagnostics([.. locked.Diagnostics]));
        }

        try
        {
            return await TaintUnderLock(app, name, cancellationToken);
        }
        finally
        {
            await locked.Value.Release(CancellationToken.None);
        }
    }

    private static async Task<int> TaintUnderLock(CliApplication app, string name, CancellationToken cancellationToken)
    {
        // Read the state
        var read = await app.State.Read(new StateReadArguments(), cancellationToken);
        if (read.IsFailure)
        {
            app.Messenger.ReportDiagnostics(new PolicyDiagnostics([.. read.Diagnostics]));
            return ExitCodes.Error;
        }

        if (read.Value.State is not { } state || state.FindScript(name) is null)
        {
            app.Messenger.Warn($"No execution is recorded for script '{name}'; there is nothing to taint.");
            return ExitCodes.Error;
        }

        // Remove the script from the state's execution history.
        state = state.RemoveScript(name);

        // Write the result.
        var written = await app.State.Write(new StateWriteArguments(state), cancellationToken);
        if (written.IsFailure)
        {
            app.Messenger.ReportDiagnostics(new PolicyDiagnostics([.. written.Diagnostics]));
            return ExitCodes.Error;
        }

        app.Messenger.Success($"Removed the recorded execution for '{name}'. It will run again on the next apply.");
        return ExitCodes.NoChanges;
    }
}
