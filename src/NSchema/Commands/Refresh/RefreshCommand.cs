using System.CommandLine;
using NSchema.Configuration;
using NSchema.Operations.Refresh;
using NSchema.Policies;
using NSchema.Services;

namespace NSchema.Commands.Refresh;

internal static class RefreshCommand
{
    public static Command Create()
    {
        var command = new Command("refresh", "Read the live schema and write it to the state store.");

        command.Options.AddRange(RefreshOptions.All);

        command.SetAction(Run);
        return command;
    }

    private static async ValueTask<RefreshConfiguration> Resolve(ParseResult result, string? environment, CancellationToken cancellationToken)
    {
        var config = await ConfigurationFactory.Load<RefreshConfiguration>(result, environment, cancellationToken);
        new RefreshConfigurationValidator().ValidateOrThrow(config);
        return config;
    }

    private static async Task<int> Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var environment = ConfigurationFactory.ResolveEnvironment(parseResult);
        var configuration = await Resolve(parseResult, environment, cancellationToken);
        using var app = CliApplicationBuilder.Create(parseResult)
            .ConfigureBackendState(configuration.State)
            .ConfigureDatabaseProvider(configuration.Provider)
            .Build();
        app.Messenger.ReportEnvironment(environment);

        // Refresh writes the live schema into the store, so it takes the lock too.
        var locked = await app.Locks.Acquire("refresh", configuration.NoLock, cancellationToken);
        if (locked.IsFailure)
        {
            app.Messenger.ReportDiagnostics(new PolicyDiagnostics([.. locked.Diagnostics]));
            return ExitCodes.Error;
        }

        if (locked.Diagnostics.Count > 0)
        {
            app.Messenger.ReportDiagnostics(new PolicyDiagnostics([.. locked.Diagnostics]));
        }

        // Release explicitly in a finally — a lock handle is not disposable (a manual lock can outlive the process).
        try
        {
            var result = await app.Operations.Refresh(new RefreshArguments(), cancellationToken);
            if (result.IsFailure)
            {
                if (result.Diagnostics.Count > 0)
                {
                    app.Messenger.ReportDiagnostics(new PolicyDiagnostics([.. result.Diagnostics]));
                }
                return ExitCodes.Error;
            }

            app.Messenger.Success($"State store updated successfully.");
            return ExitCodes.NoChanges;
        }
        finally
        {
            await locked.Value.Release(CancellationToken.None);
        }
    }
}
