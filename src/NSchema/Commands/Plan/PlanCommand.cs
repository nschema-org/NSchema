using System.CommandLine;
using NSchema.Configuration;
using NSchema.Operations.Plan;

namespace NSchema.Commands.Plan;

internal static class PlanCommand
{
    public static Command Create()
    {
        var command = new Command("plan", "Compute and show the migration plan without applying it.");

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
        using var app = CliApplicationBuilder.Create()
            .ConfigureDesiredSchema(configuration.Schema)
            .ConfigurePolicies(configuration.DestructiveActionPolicy)
            .ConfigureDatabaseProvider(configuration.Provider)
            .ConfigureBackendState(configuration.State)
            .Build();
        await app.Plan(new PlanArguments { Schemas = configuration.Scope }, cancellationToken);
    }
}
