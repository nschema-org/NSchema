using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using NSchema.Configuration;
using NSchema.Operations.Validate;
using Spectre.Console;

namespace NSchema.Commands.Validate;

internal static class ValidateCommand
{
    public static Command Create()
    {
        var command = new Command("validate", "Validate the desired schema without contacting a database or state store.");

        command.SetAction(Run);
        return command;
    }

    private static async Task Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var environment = ConfigurationFactory.ResolveEnvironment(parseResult);
        // Loading resolves --directory (chdir) and verifies the environment exists; validate has no config of its own.
        await ConfigurationFactory.Load<ValidateConfiguration>(parseResult, environment, cancellationToken);

        using var app = CliApplicationBuilder.Create()
            .ConfigureDesiredSchema(environment)
            .Build();

        app.Services.GetRequiredService<IAnsiConsole>().ReportEnvironment(environment);
        await app.Validate(new ValidateArguments(), cancellationToken);
    }
}
