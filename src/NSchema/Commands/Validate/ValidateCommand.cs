using System.CommandLine;
using NSchema.Configuration;
using NSchema.Operations.Validate;

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
        // Loading resolves --directory (chdir) so the recursive *.sql glob runs against the project root.
        // There is nothing else to configure for validate, so the loaded model is discarded.
        // Keeping the pattern is just nice for consistency, and makes it easier if we add validate options later.
        await ConfigurationFactory.Load<ValidateConfiguration>(parseResult, cancellationToken);

        using var app = CliApplicationBuilder.Create()
            .ConfigureDesiredSchema()
            .Build();
        await app.Validate(new ValidateArguments(), cancellationToken);
    }
}
