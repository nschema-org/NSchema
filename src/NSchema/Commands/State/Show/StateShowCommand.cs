using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using NSchema.Configuration;
using NSchema.Configuration.State;
using NSchema.Operations;
using NSchema.Schema;
using NSchema.Services;
using Spectre.Console;

namespace NSchema.Commands.State.Show;

internal static class StateShowCommand
{
    private static readonly Argument<string?> FileArgument = new("file")
    {
        Description = "A state file to show directly, instead of the recorded state from the configured store. " +
                      "No backend configuration is required.",
        Arity = ArgumentArity.ZeroOrOne,
    };

    public static Command Create()
    {
        var command = new Command("show", "Show the recorded schema state — from the configured store, or a state file given directly.");

        command.Arguments.Add(FileArgument);
        command.Options.AddRange(StateShowOptions.All);

        command.SetAction(Run);
        return command;
    }

    private static async ValueTask<StateShowConfiguration> Resolve(ParseResult result, string? environment, CancellationToken cancellationToken)
    {
        var config = await ConfigurationFactory.Load<StateShowConfiguration>(result, environment, cancellationToken);
        new StateShowConfigurationValidator().ValidateOrThrow(config);
        return config;
    }

    private static async Task Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        if (parseResult.GetValue(FileArgument) is { } file)
        {
            await ShowStateFile(parseResult, file, cancellationToken);
            return;
        }

        var environment = ConfigurationFactory.ResolveEnvironment(parseResult);
        var configuration = await Resolve(parseResult, environment, cancellationToken);

        using var app = CliApplicationBuilder.Create(parseResult)
            .ConfigureBackendState(configuration.State)
            .Build();
        var presenter = app.Services.GetRequiredService<IConsolePresenter>();
        presenter.ReportEnvironment(environment);

        presenter.Announce("Showing recorded state. The live database will not be contacted.");
        var schema = await app.Services.GetRequiredService<ICurrentSchemaProvider>()
            .GetSchema(SchemaSourceMode.Offline, configuration.Scope, required: true, cancellationToken);
        presenter.ReportSchema(schema);
    }

    private static async Task ShowStateFile(ParseResult parseResult, string file, CancellationToken cancellationToken)
    {
        // A state file is self-contained: point a file-backed store at it and read offline — no project config needed.
        StateShowOptions.Scope.TryGetValue(parseResult, out var scope);

        using var app = CliApplicationBuilder.Create(parseResult)
            .ConfigureBackendState(new StateConfig { File = new FileStateConfig { Path = file } })
            .Build();
        var presenter = app.Services.GetRequiredService<IConsolePresenter>();

        presenter.Announce($"Showing state file {file}.");
        var schema = await app.Services.GetRequiredService<ICurrentSchemaProvider>()
            .GetSchema(SchemaSourceMode.Offline, scope, required: true, cancellationToken);
        presenter.ReportSchema(schema);
    }
}
