using System.CommandLine;
using NSchema.Configuration;

namespace NSchema.Commands.Validate;

internal static class ValidateCommand
{
    public static Command Create()
    {
        var command = new Command("validate", "Validate the desired schema without contacting a database or state store.");

        command.Options.Add(CommonOptions.Config.Option);
        command.Options.AddRange(ValidateOptions.All);

        command.SetAction(Run);
        return command;
    }

    private static ValidateConfiguration Resolve(ParseResult result)
    {
        var config = ConfigurationFactory.Load<ValidateConfiguration>(result);
        new ValidateConfigurationValidator().ValidateOrThrow(config);
        return config;
    }

    private static async Task Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var configuration = Resolve(parseResult);
        using var app = CliApplicationBuilder.Create()
            .ConfigureDesiredSchema(configuration.Schema)
            .Build();
        await app.Validate(cancellationToken);
    }
}
