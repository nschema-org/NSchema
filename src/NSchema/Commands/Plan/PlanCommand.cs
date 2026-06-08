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

        command.Options.Add(CommonOptions.Config.Option);
        command.Options.AddRange(PlanOptions.All);

        command.SetAction(Run);
        return command;
    }

    private static PlanConfiguration Resolve(ParseResult result)
    {
        var config = ConfigurationFactory.Load<PlanConfiguration>(result);
        new PlanConfigurationValidator().ValidateOrThrow(config);
        return config;
    }

    private static async Task Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var configuration = Resolve(parseResult);

        if (configuration.Destroy)
        {
            await RunDestroy(configuration, cancellationToken);
            return;
        }

        using var app = CliApplicationBuilder.Create()
            .ConfigureDesiredSchema(configuration.Schema)
            .ConfigurePolicies(configuration.DestructiveActionPolicy)
            .ConfigureDatabaseProvider(configuration.Provider)
            .ConfigureBackendState(configuration.State)
            .Build();
        await app.Plan(new PlanArguments { Schemas = configuration.Scope }, cancellationToken);
    }

    private static async Task RunDestroy(PlanConfiguration configuration, CancellationToken cancellationToken)
    {
        var builder = CliApplicationBuilder.Create()
            .ConfigureDatabaseProvider(configuration.Provider)
            .ConfigureBackendState(configuration.State);

        // The desired schema is only the teardown source when no state store is configured; otherwise omit it so we
        // don't glob the working directory for schema files that aren't needed.
        if (configuration.HasSchema)
        {
            builder.ConfigureDesiredSchema(configuration.Schema);
        }

        using var app = builder.Build();
        await app.PlanDestroy(new PlanDestroyArguments(), cancellationToken);
    }
}
