using System.CommandLine;
using NSchema.Configuration;
using NSchema.Operations.ForceUnlock;

namespace NSchema.Commands.ForceUnlock;

internal static class ForceUnlockCommand
{
    public static Command Create()
    {
        var command = new Command("force-unlock", "Forcibly release a stale lock on the state store.");

        command.Options.AddRange(ForceUnlockOptions.All);

        command.SetAction(Run);
        return command;
    }

    private static async ValueTask<ForceUnlockConfiguration> Resolve(ParseResult result, CancellationToken cancellationToken = default)
    {
        var config = await ConfigurationFactory.Load<ForceUnlockConfiguration>(result, cancellationToken);
        new ForceUnlockConfigurationValidator().ValidateOrThrow(config);
        return config;
    }

    private static async Task Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var configuration = await Resolve(parseResult, cancellationToken);
        using var app = CliApplicationBuilder.Create()
            .ConfigureBackendState(configuration.State)
            .ConfigureConfirmation(configuration.Force)
            .Build();
        await app.ForceUnlock(new ForceUnlockArguments(), cancellationToken);
    }
}
