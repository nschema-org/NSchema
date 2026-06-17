using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using NSchema.Configuration;
using NSchema.Operations.Destroy;
using Spectre.Console;

namespace NSchema.Commands.Destroy;

internal static class DestroyCommand
{
    public static Command Create()
    {
        var command = new Command("destroy", "Drop all managed schema objects from the target database.");

        command.Options.AddRange(DestroyOptions.All);

        command.SetAction(Run);
        return command;
    }

    private static async ValueTask<DestroyConfiguration> Resolve(ParseResult result, CancellationToken cancellationToken = default)
    {
        var config = await ConfigurationFactory.Load<DestroyConfiguration>(result, cancellationToken);
        new DestroyConfigurationValidator().ValidateOrThrow(config);
        return config;
    }

    private static async Task Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var configuration = await Resolve(parseResult, cancellationToken);
        var builder = CliApplicationBuilder.Create()
            .ConfigureDatabaseProvider(configuration.Provider)
            .ConfigureBackendState(configuration.State)
            .ConfigureConfirmation(configuration.AutoApprove);

        // The working-directory schema is only the teardown source when no state store is configured; otherwise omit
        // it so we don't glob for schema files that aren't needed.
        if (!configuration.HasStateStore)
        {
            builder.ConfigureDesiredSchema(configuration.Environment);
        }

        using var app = builder.Build();
        app.Services.GetRequiredService<IAnsiConsole>().ReportEnvironment(configuration.Environment);
        await app.Destroy(new DestroyArguments(), cancellationToken);
    }
}
