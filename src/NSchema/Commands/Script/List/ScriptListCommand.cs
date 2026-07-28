using System.CommandLine;
using NSchema.State;

namespace NSchema.Commands.Script.List;

internal static class ScriptListCommand
{
    public static Command Create()
    {
        var command = new Command("list", "List the script executions recorded in the state.");

        command.SetAction(Run);
        return command;
    }

    private static Task<int> Run(ParseResult parseResult, CancellationToken cancellationToken) => CommandRunner.Run(
        parseResult,
        configure: (builder, configuration) => builder.ConfigureState(configuration.State),
        command: Execute,
        validator: new ScriptListConfigurationValidator(),
        announceEnvironment: true,
        cancellationToken: cancellationToken
    );

    private static async Task<int> Execute(CommandContext<ScriptListConfiguration> context, CancellationToken cancellationToken)
    {
        var (app, _, _, _) = context;

        var result = await app.State.Read(new StateReadArguments(), cancellationToken);
        if (result.IsFailure)
        {
            app.Messenger.ReportDiagnostics(result.Diagnostics);
            return ExitCodes.Error;
        }

        app.Messenger.ReportScripts(result.Require().State?.Scripts ?? []);
        return ExitCodes.NoChanges;
    }
}
