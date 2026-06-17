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

    private static async ValueTask<ValidateConfiguration> Resolve(ParseResult result, CancellationToken cancellationToken = default)
    {
        var config = await ConfigurationFactory.Load<ValidateConfiguration>(result, cancellationToken);
        //new ValidateConfigurationValidator().ValidateOrThrow(config);
        return config;
    }

    private static async Task Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var configuration = await Resolve(parseResult, cancellationToken);

        using var app = CliApplicationBuilder.Create()
            .ConfigureDesiredSchema(configuration.Environment)
            .Build();

        app.Services.GetRequiredService<IAnsiConsole>().ReportEnvironment(configuration.Environment);
        await app.Validate(new ValidateArguments(), cancellationToken);
    }
}
