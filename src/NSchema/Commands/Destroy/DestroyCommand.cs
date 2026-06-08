using System.CommandLine;
using NSchema.Configuration;
using NSchema.Configuration.Provider;
using NSchema.Configuration.Schema;
using NSchema.Configuration.State;

namespace NSchema.Commands.Destroy;

internal static class DestroyCommand
{
    public static Command Create()
    {
        var command = new Command("destroy", "Drop all managed schema objects from the target database.");

        command.Options.Add(CommonOptions.Config.Option);
        command.Options.Add(CommonOptions.Scope.Option);
        command.Options.Add(CommonOptions.AutoApprove.Option);

        command.Options.AddRange(ProviderOptions.All);
        command.Options.AddRange(StateOptions.All);
        command.Options.AddRange(SchemaOptions.All);

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
            .ConfigureScope(configuration.Scope)
            .ConfigureDatabaseProvider(configuration.Provider)
            .ConfigureBackendState(configuration.State)
            .ConfigureConfirmation(configuration.AutoApprove);

        // The desired schema is only the teardown source when no state store is configured; otherwise omit it so we
        // don't glob the working directory for schema files that aren't needed.
        if (configuration.HasSchema)
        {
            builder.ConfigureDesiredSchema(configuration.Schema);
        }

        using var app = builder.Build();
        await app.Destroy(cancellationToken);
    }
}
