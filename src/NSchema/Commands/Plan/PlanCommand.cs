using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using NSchema.Configuration;
using NSchema.Operations.Plan;
using NSchema.Operations.PlanDestroy;
using NSchema.Services;

namespace NSchema.Commands.Plan;

internal static class PlanCommand
{
    public static Command Create()
    {
        var command = new Command("plan", "Compute and show the migration plan without applying it. Use --destroy to preview a teardown instead.");

        command.Options.AddRange(PlanOptions.All);
        command.Subcommands.Add(PlanShowCommand.Create());

        command.SetAction(Run);
        return command;
    }

    private static async ValueTask<PlanConfiguration> Resolve(ParseResult result, string? environment, CancellationToken cancellationToken)
    {
        var config = await ConfigurationFactory.Load<PlanConfiguration>(result, environment, cancellationToken);
        new PlanConfigurationValidator().ValidateOrThrow(config);
        return config;
    }

    private static async Task<int> Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var environment = ConfigurationFactory.ResolveEnvironment(parseResult);
        var configuration = await Resolve(parseResult, environment, cancellationToken);

        if (configuration.Destroy)
        {
            return await RunDestroy(parseResult, configuration, environment, cancellationToken);
        }

        using var app = CliApplicationBuilder.Create(parseResult)
            .ConfigureDesiredSchema(environment)
            .ConfigurePolicies(configuration.DestructiveActionPolicy)
            .ConfigureDatabaseProvider(configuration.Provider)
            .ConfigureBackendState(configuration.State)
            .Build();
        app.Services.GetRequiredService<IConsolePresenter>().ReportEnvironment(environment);
        var result = await app.Plan(new PlanArguments { Schemas = configuration.Scope, OutFile = configuration.OutFile }, cancellationToken);
        return ExitCode(result, configuration.DetailedExitCode);
    }

    private static async Task<int> RunDestroy(ParseResult parseResult, PlanConfiguration configuration, string? environment, CancellationToken cancellationToken)
    {
        var builder = CliApplicationBuilder.Create(parseResult)
            .ConfigureDatabaseProvider(configuration.Provider)
            .ConfigureBackendState(configuration.State);

        // The working-directory schema is only the teardown source when no state store is configured; otherwise omit
        // it so we don't glob for schema files that aren't needed.
        if (!configuration.HasStateStore)
        {
            builder.ConfigureDesiredSchema(environment);
        }

        using var app = builder.Build();
        app.Services.GetRequiredService<IConsolePresenter>().ReportEnvironment(environment);
        var result = await app.PlanDestroy(new PlanDestroyArguments { OutFile = configuration.OutFile }, cancellationToken);
        return ExitCode(result, configuration.DetailedExitCode);
    }

    private static int ExitCode(PlanResult result, bool detailed) =>
        detailed && result.HasChanges ? ExitCodes.HasChanges : ExitCodes.NoChanges;
}
