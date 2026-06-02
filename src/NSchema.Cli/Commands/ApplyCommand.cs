using System.CommandLine;
using NSchema.Cli.Configuration;

namespace NSchema.Cli.Commands;

internal static class ApplyCommand
{
    public static Command Create()
    {
        var command = new Command("apply", "Compute the plan and apply it to the target database.");
        command.Options.Add(CliOptions.Format);
        command.Options.Add(CliOptions.SchemaDir);
        command.Options.Add(CliOptions.SchemaGlob);
        command.Options.Add(CliOptions.AutoApprove);
        command.SetAction(Apply);
        return command;
    }

    private static async Task Apply(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var configuration = NSchemaConfigurationFactory.Create(parseResult);
        using var app = CliApplicationBuilder.Create(configuration)
            .ConfigureDesiredSchema()
            .ConfigureScope()
            .ConfigurePolicies()
            .ConfigureDatabaseProvider()
            .ConfigureBackendState()
            .ConfigureConfirmation()
            .Build();
        await app.Apply(cancellationToken);
    }
}
