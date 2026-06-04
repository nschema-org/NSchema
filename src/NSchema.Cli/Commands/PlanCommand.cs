using System.CommandLine;
using NSchema.Cli.Configuration;

namespace NSchema.Cli.Commands;

internal static class PlanCommand
{
    public static Command Create()
    {
        var command = new Command("plan", "Compute and show the migration plan without applying it.");
        command.Options.Add(CliOptions.Schema.Format);
        command.Options.Add(CliOptions.Schema.Directory);
        command.Options.Add(CliOptions.Schema.Pattern);
        command.Options.Add(CliOptions.Migration.Scope);
        command.Options.Add(CliOptions.Migration.Destructive);
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
