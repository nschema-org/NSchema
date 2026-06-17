using System.CommandLine;
using NSchema.Configuration;
using NSchema.Operations.Plan;
using NSchema.Operations.PlanDestroy;

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

    private static async ValueTask<PlanConfiguration> Resolve(ParseResult result, CancellationToken cancellationToken)
    {
        var config = await ConfigurationFactory.Load<PlanConfiguration>(result, cancellationToken);
        new PlanConfigurationValidator().ValidateOrThrow(config);
        return config;
    }

    private static async Task Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var configuration = await Resolve(parseResult, cancellationToken);

        if (configuration.Destroy)
        {
            await RunDestroy(configuration, cancellationToken);
            return;
        }

        using var app = CliApplicationBuilder.Create()
            .ConfigureDesiredSchema(configuration.Environment)
            .ConfigureScripts()
            .ConfigurePolicies(configuration.DestructiveActionPolicy)
            .ConfigureDatabaseProvider(configuration.Provider)
            .ConfigureBackendState(configuration.State)
            .Build();
        await app.Plan(new PlanArguments { Schemas = configuration.Scope, OutFile = configuration.OutFile }, cancellationToken);
    }

    private static async Task RunDestroy(PlanConfiguration configuration, CancellationToken cancellationToken)
    {
        var builder = CliApplicationBuilder.Create()
            .ConfigureDatabaseProvider(configuration.Provider)
            .ConfigureBackendState(configuration.State);

        // The working-directory schema is only the teardown source when no state store is configured; otherwise omit
        // it so we don't glob for schema files that aren't needed.
        if (!configuration.HasStateStore)
        {
            builder.ConfigureDesiredSchema(configuration.Environment);
        }

        using var app = builder.Build();
        await app.PlanDestroy(new PlanDestroyArguments { OutFile = configuration.OutFile }, cancellationToken);
    }
}
