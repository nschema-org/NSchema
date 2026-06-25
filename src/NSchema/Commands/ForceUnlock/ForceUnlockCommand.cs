using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using NSchema.Configuration;
using NSchema.Operations.ForceUnlock;
using Spectre.Console;

namespace NSchema.Commands.ForceUnlock;

internal static class ForceUnlockCommand
{
    private static readonly Argument<string?> LockIdArgument = new("lock-id")
    {
        Description = "The id of the lock to release, taken from the error of the blocked operation. When given, the " +
                      "unlock is refused if it no longer matches the held lock (a safety check). Omit to release " +
                      "whatever lock is held.",
        Arity = ArgumentArity.ZeroOrOne,
    };

    public static Command Create()
    {
        var command = new Command("force-unlock", "Forcibly release a stale lock on the state store.");

        command.Arguments.Add(LockIdArgument);
        command.Options.AddRange(ForceUnlockOptions.All);

        command.SetAction(Run);
        return command;
    }

    private static async ValueTask<ForceUnlockConfiguration> Resolve(ParseResult result, string? environment, CancellationToken cancellationToken = default)
    {
        var config = await ConfigurationFactory.Load<ForceUnlockConfiguration>(result, environment, cancellationToken);
        new ForceUnlockConfigurationValidator().ValidateOrThrow(config);
        return config;
    }

    private static async Task Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var environment = ConfigurationFactory.ResolveEnvironment(parseResult);
        var configuration = await Resolve(parseResult, environment, cancellationToken);
        using var app = CliApplicationBuilder.Create(parseResult)
            .ConfigureBackendState(configuration.State)
            .ConfigureConfirmation(configuration.Force)
            .Build();
        app.Services.GetRequiredService<IAnsiConsole>().ReportEnvironment(environment);
        await app.ForceUnlock(new ForceUnlockArguments { ExpectedLockId = parseResult.GetValue(LockIdArgument) }, cancellationToken);
    }
}
