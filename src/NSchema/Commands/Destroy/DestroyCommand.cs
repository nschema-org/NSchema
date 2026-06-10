using System.CommandLine;
using NSchema.Configuration;
using NSchema.Operations.Destroy;

namespace NSchema.Commands.Destroy;

internal static class DestroyCommand
{
    public static Command Create()
    {
        var command = new Command("destroy", "Drop all managed schema objects from the target database.");

        command.Options.Add(CommonOptions.Config.Option);
        command.Options.AddRange(DestroyOptions.All);

        command.SetAction(Run);
        return command;
    }

    private static DestroyConfiguration Resolve(ParseResult result)
    {
        var config = ConfigurationFactory.Load<DestroyConfiguration>(result);
        new DestroyConfigurationValidator().ValidateOrThrow(config);
        return config;
    }

    private static async Task Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var configuration = Resolve(parseResult);
        var builder = CliApplicationBuilder.Create()
            .ConfigureDatabaseProvider(configuration.Provider)
            .ConfigureBackendState(configuration.State)
            .ConfigureConfirmation(configuration.AutoApprove);

        // The working-directory schema is only the teardown source when no state store is configured; otherwise omit
        // it so we don't glob for schema files that aren't needed.
        if (!configuration.HasStateStore)
        {
            builder.ConfigureDesiredSchema();
        }

        using var app = builder.Build();
        await app.Destroy(new DestroyArguments(), cancellationToken);
    }
}
