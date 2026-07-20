using System.CommandLine;
using NSchema.Configuration;
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

    private static async Task<int> Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var environment = ConfigurationFactory.ResolveEnvironment(parseResult);
        // Loading resolves --directory (chdir) and verifies the environment exists; validate has no config of its own.
        await ConfigurationFactory.Load<ValidateConfiguration>(parseResult, environment, cancellationToken);

        using var app = CliApplicationBuilder.Create(parseResult)
            .ConfigureDesiredSchema()
            .Build();

        app.Messenger.ReportEnvironment(environment);

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
