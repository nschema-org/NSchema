using System.CommandLine;
using NSchema.Configuration;
using NSchema.Configuration.Plugins;
using NSchema.Operations.Doctor;
using NSchema.Policies;
using NSchema.Services;

namespace NSchema.Commands.Doctor;

internal static class DoctorCommand
{
    public static Command Create()
    {
        var command = new Command("doctor", "Check that the configured database and state store are reachable and healthy.");

        command.SetAction(Run);
        return command;
    }

    private static async ValueTask<DoctorConfiguration> Resolve(ParseResult result, string? environment, CancellationToken cancellationToken = default)
    {
        var config = await ConfigurationFactory.Load<DoctorConfiguration>(result, environment, cancellationToken);
        new DoctorConfigurationValidator().ValidateOrThrow(config);
        return config;
    }

    private static async Task<int> Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var environment = ConfigurationFactory.ResolveEnvironment(parseResult);
        var configuration = await Resolve(parseResult, environment, cancellationToken);

        // Configure the plugins without throwing, so a misconfigured one becomes a reportable diagnostic — doctor's
        // whole job — rather than aborting the health check on the first failure.
        var builder = CliApplicationBuilder.Create(parseResult);
        var diagnostics = new[]
        {
            builder.TryConfigureDatabaseProvider(configuration.Provider),
            builder.TryConfigureBackendState(configuration.State),
        }.OfType<PluginDiagnostic>().ToList();
        using var app = builder.Build();

        app.Messenger.ReportEnvironment(environment);

        if (diagnostics.Count > 0)
        {
            // The plugins didn't fully wire up, so the live database/state checks can't run meaningfully — report every
            // plugin problem and stop, rather than following them with misleading "not configured" lines.
            foreach (var diagnostic in diagnostics)
            {
                app.Messenger.Warn($"Plugin '{diagnostic.Label}': {string.Join("; ", diagnostic.Errors)}");
            }

            return ExitCodes.Error;
        }

        // The operation aggregates every check into one result; the CLI renders the checks (by severity) and a final
        // pass/fail line, then maps the outcome to an exit code.
        var result = await app.Operations.Doctor(new DoctorArguments(), cancellationToken);

        // A failed result means the doctor run itself could not complete (distinct from a probe finding a problem);
        // surface its operation-level diagnostics and stop.
        if (result.IsFailure)
        {
            if (result.Diagnostics.Count > 0)
            {
                app.Messenger.ReportDiagnostics(new PolicyDiagnostics([.. result.Diagnostics]));
            }

            return ExitCodes.Error;
        }

        // The run completed; the checks are the deliverable. Render them, then map their severity to an exit code.
        var report = result.Value;
        if (report.Checks.Count > 0)
        {
            app.Messenger.ReportDiagnostics(new PolicyDiagnostics([.. report.Checks]));
        }

        if (report.HasErrors)
        {
            return ExitCodes.Error;
        }

        app.Messenger.Success($"All checks passed.");
        return ExitCodes.NoChanges;
    }
}
