using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using NSchema.Configuration;
using NSchema.Operations.Show;
using Spectre.Console;

namespace NSchema.Commands.Show;

internal static class ShowCommand
{
    private static readonly Argument<string?> PlanFileArgument = new("planfile")
    {
        Description = "A saved plan file (from `plan --out`) to show instead of the recorded state. " +
                      "Shows its diff, plan, and SQL; no state store is required.",
        Arity = ArgumentArity.ZeroOrOne,
    };

    public static Command Create()
    {
        var command = new Command("show", "Show the schema recorded in the state store, or a saved plan file, without contacting the live database.");

        command.Arguments.Add(PlanFileArgument);
        command.Options.AddRange(ShowOptions.All);

        command.SetAction(Run);
        return command;
    }

    private static async ValueTask<ShowConfiguration> Resolve(ParseResult result, string? environment, CancellationToken cancellationToken)
    {
        var config = await ConfigurationFactory.Load<ShowConfiguration>(result, environment, cancellationToken);
        new ShowConfigurationValidator().ValidateOrThrow(config);
        return config;
    }

    private static async Task Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        if (parseResult.GetValue(PlanFileArgument) is { } planFile)
        {
            await ShowPlanFile(parseResult, planFile, cancellationToken);
            return;
        }

        var environment = ConfigurationFactory.ResolveEnvironment(parseResult);
        var configuration = await Resolve(parseResult, environment, cancellationToken);
        using var app = CliApplicationBuilder.Create(parseResult)
            .ConfigureBackendState(configuration.State)
            .Build();
        app.Services.GetRequiredService<IAnsiConsole>().ReportEnvironment(environment);
        await app.Show(new ShowArguments { Schemas = configuration.Scope }, cancellationToken);
    }

    private static async Task ShowPlanFile(ParseResult parseResult, string planFile, CancellationToken cancellationToken)
    {
        // A saved plan is self-contained: no project config, state store, live provider, or environment is needed,
        // so we skip configuration loading entirely and just read the file.
        using var app = CliApplicationBuilder.Create(parseResult).Build();
        await app.Show(new ShowArguments { PlanFile = planFile }, cancellationToken);
    }
}
