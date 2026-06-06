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
        command.Options.Add(CommonOptions.Scope);
        command.Options.Add(CommonOptions.Destructive);

        command.Options.Add(ProviderOptions.Type);
        command.Options.Add(ProviderOptions.ConnectionString);

        command.Options.Add(StateOptions.File);
        command.Options.Add(StateOptions.S3Bucket);
        command.Options.Add(StateOptions.S3Key);

        command.Options.Add(SchemaOptions.Format);
        command.Options.Add(SchemaOptions.Directory);
        command.Options.Add(SchemaOptions.Pattern);

        command.SetAction(Run);
        return command;
    }

    private static PlanConfiguration Resolve(ParseResult result)
    {
        var config = NSchemaConfigurationFactory.Load<PlanConfiguration>(result);
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
