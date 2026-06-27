using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using NSchema.Configuration;
using NSchema.Operations.Refresh;
using NSchema.Services;

namespace NSchema.Commands.Refresh;

internal static class RefreshCommand
{
    public static Command Create()
    {
        var command = new Command("refresh", "Read the live schema and write it to the state store.");

        command.Options.AddRange(RefreshOptions.All);

        command.SetAction(Run);
        return command;
    }

    private static async ValueTask<RefreshConfiguration> Resolve(ParseResult result, string? environment, CancellationToken cancellationToken)
    {
        var config = await ConfigurationFactory.Load<RefreshConfiguration>(result, environment, cancellationToken);
        new RefreshConfigurationValidator().ValidateOrThrow(config);
        return config;
    }

    private static async Task Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var environment = ConfigurationFactory.ResolveEnvironment(parseResult);
        var configuration = await Resolve(parseResult, environment, cancellationToken);
        using var app = CliApplicationBuilder.Create(parseResult)
            .ConfigureBackendState(configuration.State)
            .ConfigureDatabaseProvider(configuration.Provider)
            .Build();
        app.Services.GetRequiredService<IConsolePresenter>().ReportEnvironment(environment);
        await app.Refresh(new RefreshArguments { SkipLock = configuration.NoLock }, cancellationToken);
    }
}
