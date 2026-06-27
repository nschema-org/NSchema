using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using NSchema.Configuration;
using NSchema.Operations.Destroy;
using NSchema.Services;

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

    private static async ValueTask<DestroyConfiguration> Resolve(ParseResult result, string? environment, CancellationToken cancellationToken = default)
    {
        var config = await ConfigurationFactory.Load<DestroyConfiguration>(result, environment, cancellationToken);
        new DestroyConfigurationValidator().ValidateOrThrow(config);
        return config;
    }

    private static async Task Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var environment = ConfigurationFactory.ResolveEnvironment(parseResult);
        var configuration = await Resolve(parseResult, environment, cancellationToken);
        var builder = CliApplicationBuilder.Create(parseResult)
            .ConfigureDatabaseProvider(configuration.Provider)
            .ConfigureBackendState(configuration.State)
            .ConfigureConfirmation(configuration.AutoApprove);

        // The working-directory schema is only the teardown source when no state store is configured; otherwise omit
        // it so we don't glob for schema files that aren't needed.
        if (!configuration.HasStateStore)
        {
            builder.ConfigureDesiredSchema(environment);
        }

        using var app = builder.Build();
        app.Services.GetRequiredService<IConsolePresenter>().ReportEnvironment(environment);
        await app.Destroy(new DestroyArguments { SkipLock = configuration.NoLock }, cancellationToken);
    }
}
