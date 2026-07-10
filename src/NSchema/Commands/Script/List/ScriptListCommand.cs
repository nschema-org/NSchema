using System.CommandLine;
using NSchema.Configuration;
using NSchema.Policies;
using NSchema.State.Storage;

namespace NSchema.Commands.Script.List;

internal static class ScriptListCommand
{
    public static Command Create()
    {
        var command = new Command("list", "List the run-once script executions recorded in the state.");

        command.SetAction(Run);
        return command;
    }

    private static async ValueTask<ScriptListConfiguration> Resolve(ParseResult result, string? environment, CancellationToken cancellationToken)
    {
        var config = await ConfigurationFactory.Load<ScriptListConfiguration>(result, environment, cancellationToken);
        new ScriptListConfigurationValidator().ValidateOrThrow(config);
        return config;
    }

    private static async Task<int> Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var environment = ConfigurationFactory.ResolveEnvironment(parseResult);
        var configuration = await Resolve(parseResult, environment, cancellationToken);

        using var app = CliApplicationBuilder.Create(parseResult)
            .ConfigureBackendState(configuration.State)
            .Build();
        app.Messenger.ReportEnvironment(environment);

        var result = await app.State.Read(new StateReadArguments(), cancellationToken);
        if (result.IsFailure)
        {
            app.Messenger.ReportDiagnostics(new PolicyDiagnostics([.. result.Diagnostics]));
            return ExitCodes.Error;
        }

        app.Messenger.ReportScripts(result.Value.State?.Scripts ?? []);
        return ExitCodes.NoChanges;
    }
}
