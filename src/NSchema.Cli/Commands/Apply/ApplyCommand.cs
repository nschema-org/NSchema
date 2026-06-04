using System.CommandLine;
using NSchema.Cli.Configuration;
using NSchema.Cli.Extensions;

namespace NSchema.Cli.Commands.Apply;

internal static class ApplyCommand
{
    public static Command Create()
    {
        var command = new Command("apply", "Compute the plan and apply it to the target database.");

        command.Options.Add(CliOptions.Global.Config);

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

        command.Options.Add(CliOptions.Apply.AutoApprove);
        command.SetAction(Run);
        return command;
    }

    private static ApplyConfiguration Resolve(ParseResult result)
    {
        var config = NSchemaConfigurationFactory.Create(result);
        var configuration = new ApplyConfiguration
        {
            Schema = config.Schema,
            Provider = config.Provider,
            State = config.State,
            Scope = config.Scope,
            DestructiveActionPolicy = config.DestructiveActionPolicy,
            AutoApprove = config.AutoApprove,
        };

        new ApplyConfigurationValidator().ValidateOrThrow(configuration);
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
            .ConfigureConfirmation(configuration.AutoApprove)
            .Build();
        await app.Apply(cancellationToken);
    }
}
