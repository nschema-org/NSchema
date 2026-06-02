using System.CommandLine;
using NSchema.Cli.Configuration;

namespace NSchema.Cli.Commands;

internal static class PlanCommand
{
    public static Command Create()
    {
        var command = new Command("plan", "Compute and show the migration plan without applying it.");
        command.Options.Add(CliOptions.Desired.Format);
        command.Options.Add(CliOptions.Desired.SchemaDir);
        command.Options.Add(CliOptions.Desired.SchemaGlob);
        command.Options.Add(CliOptions.Desired.Scope);
        command.SetAction(Plan);
        return command;
    }

    private static async Task Plan(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var configuration = NSchemaConfigurationFactory.Create(parseResult);
        using var app = CliApplicationBuilder.Create(configuration)
            .ConfigureDesiredSchema()
            .ConfigureScope()
            .ConfigurePolicies()
            .ConfigureDatabaseProvider()
            .ConfigureBackendState()
            .Build();
        await app.Plan(cancellationToken);
    }
}
