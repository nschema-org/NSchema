using System.CommandLine;
using NSchema.Configuration;
using NSchema.Operations.ForceUnlock;

namespace NSchema.Commands.ForceUnlock;

internal static class ForceUnlockCommand
{
    public static Command Create()
    {
        var command = new Command("force-unlock", "Forcibly release a stale lock on the state store.");

        command.Options.Add(CommonOptions.Config.Option);
        command.Options.AddRange(ForceUnlockOptions.All);

        command.SetAction(Run);
        return command;
    }

    private static ForceUnlockConfiguration Resolve(ParseResult result)
    {
        var config = ConfigurationFactory.Load<ForceUnlockConfiguration>(result);
        new ForceUnlockConfigurationValidator().ValidateOrThrow(config);
        return config;
    }

    private static async Task Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var configuration = Resolve(parseResult);
        using var app = CliApplicationBuilder.Create()
            .ConfigureBackendState(configuration.State)
            .ConfigureConfirmation(configuration.Force)
            .Build();
        await app.ForceUnlock(new ForceUnlockArguments(), cancellationToken);
    }
}
