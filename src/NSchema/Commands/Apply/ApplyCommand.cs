using System.CommandLine;
using NSchema.Configuration;

namespace NSchema.Commands.Apply;

internal static class ApplyCommand
{
    public static Command Create()
    {
        var command = new Command("apply", "Compute the plan and apply it to the target database.");

        command.Options.Add(CommonOptions.Config.Option);
        command.Options.AddRange(ApplyOptions.All);

        command.SetAction(Run);
        return command;
    }

    private static ApplyConfiguration Resolve(ParseResult result)
    {
        var config = ConfigurationFactory.Load<ApplyConfiguration>(result);
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
