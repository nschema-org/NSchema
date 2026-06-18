using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using NSchema.Configuration;
using NSchema.Operations.Apply;
using Spectre.Console;

namespace NSchema.Commands.Apply;

internal static class ApplyCommand
{
    public static Command Create()
    {
        var command = new Command("apply", "Compute the plan and apply it to the target database.");

        command.Options.AddRange(ApplyOptions.All);

        command.SetAction(Run);
        return command;
    }

    private static async ValueTask<ApplyConfiguration> Resolve(ParseResult result, string? environment, CancellationToken cancellationToken = default)
    {
        var config = await ConfigurationFactory.Load<ApplyConfiguration>(result, environment, cancellationToken);
        new ApplyConfigurationValidator().ValidateOrThrow(config);
        return config;
    }

    private static async Task Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var environment = ConfigurationFactory.ResolveEnvironment(parseResult);
        var configuration = await Resolve(parseResult, environment, cancellationToken);

        var builder = CliApplicationBuilder.Create(parseResult)
            .ConfigureDatabaseProvider(configuration.Provider)
            .ConfigureBackendState(configuration.State)
            .ConfigureConfirmation(configuration.AutoApprove);

        // Replaying a saved plan runs exactly the SQL captured at plan time, so the desired schema, deployment
        // scripts, and destructive-action policy that shaped that plan are not consulted again — and the *.sql files
        // needn't even be present. A fresh apply computes the plan now, so it configures all three.
        if (configuration.PlanFile is null)
        {
            builder
                .ConfigureDesiredSchema(environment)
                .ConfigurePolicies(configuration.DestructiveActionPolicy);
        }

        using var app = builder.Build();
        app.Services.GetRequiredService<IAnsiConsole>().ReportEnvironment(environment);
        await app.Apply(new ApplyArguments { Schemas = configuration.Scope, PlanFile = configuration.PlanFile }, cancellationToken);
    }
}
