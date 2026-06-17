using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using NSchema.Configuration;
using NSchema.Operations.Plan;
using NSchema.Operations.PlanDestroy;
using Spectre.Console;

namespace NSchema.Commands.Plan;

internal static class PlanCommand
{
    public static Command Create()
    {
        var command = new Command("plan", "Compute and show the migration plan without applying it. Use --destroy to preview a teardown instead.");

        command.Options.AddRange(PlanOptions.All);

        command.SetAction(Run);
        return command;
    }

    private static async ValueTask<PlanConfiguration> Resolve(ParseResult result, string? environment, CancellationToken cancellationToken)
    {
        var config = await ConfigurationFactory.Load<PlanConfiguration>(result, environment, cancellationToken);
        new PlanConfigurationValidator().ValidateOrThrow(config);
        return config;
    }

    private static async Task Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var environment = ConfigurationFactory.ResolveEnvironment(parseResult);
        var configuration = await Resolve(parseResult, environment, cancellationToken);

        if (configuration.Destroy)
        {
            await RunDestroy(configuration, environment, cancellationToken);
            return;
        }

        using var app = CliApplicationBuilder.Create()
            .ConfigureDesiredSchema(environment)
            .ConfigurePolicies(configuration.DestructiveActionPolicy)
            .ConfigureDatabaseProvider(configuration.Provider)
            .ConfigureBackendState(configuration.State)
            .Build();
        app.Services.GetRequiredService<IAnsiConsole>().ReportEnvironment(environment);
        await app.Plan(new PlanArguments { Schemas = configuration.Scope, OutFile = configuration.OutFile }, cancellationToken);
    }

    private static async Task RunDestroy(PlanConfiguration configuration, string? environment, CancellationToken cancellationToken)
    {
        var builder = CliApplicationBuilder.Create()
            .ConfigureDatabaseProvider(configuration.Provider)
            .ConfigureBackendState(configuration.State);

        // The working-directory schema is only the teardown source when no state store is configured; otherwise omit
        // it so we don't glob for schema files that aren't needed.
        if (!configuration.HasStateStore)
        {
            builder.ConfigureDesiredSchema(environment);
        }

        using var app = builder.Build();
        app.Services.GetRequiredService<IAnsiConsole>().ReportEnvironment(environment);
        await app.PlanDestroy(new PlanDestroyArguments { OutFile = configuration.OutFile }, cancellationToken);
    }
}
