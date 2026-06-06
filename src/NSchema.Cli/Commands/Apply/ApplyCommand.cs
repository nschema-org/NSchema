using System.CommandLine;
using NSchema.Cli.Configuration;
using NSchema.Cli.Configuration.Provider;
using NSchema.Cli.Configuration.Schema;
using NSchema.Cli.Configuration.State;
using NSchema.Cli.Extensions;

namespace NSchema.Cli.Commands.Apply;

internal static class ApplyCommand
{
    public static Command Create()
    {
        var command = new Command("apply", "Compute the plan and apply it to the target database.");

        command.Options.Add(CommonOptions.Config);
        command.Options.Add(CommonOptions.Scope.Option);
        command.Options.Add(CommonOptions.Destructive.Option);

        command.Options.AddRange(ProviderOptions.All);
        command.Options.AddRange(StateOptions.All);
        command.Options.AddRange(SchemaOptions.All);

        command.Options.Add(ApplyOptions.AutoApprove.Option);
        command.SetAction(Run);
        return command;
    }

    private static ApplyConfiguration Resolve(ParseResult result)
    {
        var config = NSchemaConfigurationFactory.Load<ApplyConfiguration>(result);
        new ApplyConfigurationValidator().ValidateOrThrow(config);
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
            .ConfigureConfirmation(configuration.AutoApprove)
            .Build();
        await app.Apply(cancellationToken);
    }
}
