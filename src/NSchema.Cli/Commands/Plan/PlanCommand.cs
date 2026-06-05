using System.CommandLine;
using NSchema.Cli.Configuration;
using NSchema.Cli.Extensions;

namespace NSchema.Cli.Commands.Plan;

internal static class PlanCommand
{
    public static Command Create()
    {
        var command = new Command("plan", "Compute and show the migration plan without applying it.");

        command.Options.Add(CliOptions.Common.Config);

        command.Options.Add(CliOptions.Provider.Type);
        command.Options.Add(CliOptions.Provider.ConnectionString);

        command.Options.Add(CliOptions.State.File);
        command.Options.Add(CliOptions.State.S3Bucket);
        command.Options.Add(CliOptions.State.S3Key);

        command.Options.Add(CliOptions.Schema.Format);
        command.Options.Add(CliOptions.Schema.Directory);
        command.Options.Add(CliOptions.Schema.Pattern);

        command.Options.Add(CliOptions.Migration.Scope);
        command.Options.Add(CliOptions.Migration.Destructive);

        command.SetAction(Run);
        return command;
    }

    private static PlanConfiguration Resolve(ParseResult result)
    {
        var config = NSchemaConfigurationFactory.Create(result);
        var configuration = new PlanConfiguration
        {
            Schema = config.Schema,
            Provider = config.Provider,
            State = config.State,
            Scope = config.Scope,
            DestructiveActionPolicy = config.DestructiveActionPolicy,
        };

        new PlanConfigurationValidator().ValidateOrThrow(configuration);
        return configuration;
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
