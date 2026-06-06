using System.CommandLine;
using NSchema.Cli.Configuration;
using NSchema.Cli.Configuration.Provider;
using NSchema.Cli.Configuration.Schema;
using NSchema.Cli.Configuration.State;
using NSchema.Cli.Extensions;

namespace NSchema.Cli.Commands.Plan;

internal static class PlanCommand
{
    public static Command Create()
    {
        var command = new Command("plan", "Compute and show the migration plan without applying it.");

        command.Options.Add(CommonOptions.Config);
        command.Options.Add(CommonOptions.Scope.Option);
        command.Options.Add(CommonOptions.Destructive.Option);

        command.Options.AddRange(ProviderOptions.All);
        command.Options.AddRange(StateOptions.All);
        command.Options.AddRange(SchemaOptions.All);

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
            .ConfigureScope(configuration.Scope)
            .ConfigurePolicies(configuration.DestructiveActionPolicy)
            .ConfigureDatabaseProvider(configuration.Provider)
            .ConfigureBackendState(configuration.State)
            .Build();
        await app.Plan(cancellationToken);
    }
}
