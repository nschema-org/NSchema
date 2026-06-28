using System.CommandLine;
using NSchema.Configuration;
using NSchema.Operations.Validate;
using NSchema.Policies;
using NSchema.Services;

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
            .ConfigureDesiredSchema(environment)
            .Build();

        app.Messenger.ReportEnvironment(environment);

        var result = await app.Operations.Validate(new ValidateArguments(), cancellationToken);

        // A failed result means the validation itself could not run (distinct from finding problems); surface its
        // operation-level diagnostics and stop.
        if (result.IsFailure)
        {
            if (result.Diagnostics.Count > 0)
            {
                app.Messenger.ReportDiagnostics(new PolicyDiagnostics([.. result.Diagnostics]));
            }

            return ExitCodes.Error;
        }

        // The validation ran; its findings are the deliverable. Render them, then map their severity to an exit code.
        var validation = result.Value;
        if (validation.Findings.Count > 0)
        {
            app.Messenger.ReportDiagnostics(new PolicyDiagnostics([.. validation.Findings]));
        }

        if (validation.HasErrors)
        {
            return ExitCodes.Error;
        }

        app.Messenger.Success($"Schema is valid.");
        return ExitCodes.NoChanges;
    }
}
