using System.CommandLine;
using NSchema.Operations;

namespace NSchema.Commands.Validate;

internal static class ValidateCommand
{
    public static Command Create()
    {
        var command = new Command("validate", "Validate the desired schema without contacting a database or state store.");

        command.SetAction(Run);
        return command;
    }

    private static Task<int> Run(ParseResult parseResult, CancellationToken cancellationToken) => CommandRunner.Run<ValidateConfiguration>(
        parseResult,
        configure: (b, _) => b.ConfigureDesiredSchema(),
        command: Execute,
        validator: null,
        announceEnvironment: true,
        cancellationToken: cancellationToken
    );

    private static async Task<int> Execute(CommandContext<ValidateConfiguration> context, CancellationToken cancellationToken)
    {
        var app = context.App;

        var result = await app.Operations.Validate(new ValidateArguments(), cancellationToken);

        // The findings ride the result's diagnostics; render them, then map their severity to an exit code.
        if (result.Diagnostics.Count > 0)
        {
            app.Messenger.ReportDiagnostics(result.Diagnostics);
        }

        if (result.IsFailure)
        {
            return ExitCodes.Error;
        }

        app.Messenger.Success($"Schema is valid.");
        return ExitCodes.NoChanges;
    }
}
