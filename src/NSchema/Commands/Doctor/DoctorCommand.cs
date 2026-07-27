using System.CommandLine;
using NSchema.Configuration;
using NSchema.Operations;
using NSchema.Services.Reporting;

namespace NSchema.Commands.Doctor;

internal static class DoctorCommand
{
    public static Command Create()
    {
        var command = new Command("doctor", "Check that the configured database and state store are reachable and healthy.");

        command.SetAction(Run);
        return command;
    }

    private static async Task<int> Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var messenger = ReporterFactory.CreateMessenger(parseResult);
        var environment = ConfigurationFactory.ResolveEnvironment(parseResult);

        var resolved = await ConfigurationFactory.Load(parseResult, environment, new DoctorConfigurationValidator(), cancellationToken);
        if (resolved.ReportFailure(messenger))
        {
            return ExitCodes.Error;
        }

        var configuration = resolved.Require();

        // Configure the plugins.
        var builder = CliApplicationBuilder.Create(parseResult);
        var results = new[]
        {
            builder.TryConfigureDatabase(configuration.Database),
            builder.TryConfigureState(configuration.State),
        };

        var built = builder.Build();
        if (built.ReportFailure(messenger))
        {
            return ExitCodes.Error;
        }

        using var app = built.Require();
        app.Messenger.ReportEnvironment(environment);

        var failures = results.SelectMany(result => result.Errors).ToList();
        if (failures.Count > 0)
        {
            // The plugins didn't fully wire up, so the live database/state checks can't run meaningfully — report every
            // plugin problem and stop, rather than following them with misleading "not configured" lines.
            foreach (var plugin in failures.GroupBy(error => error.Source))
            {
                app.Messenger.Warn($"Plugin '{plugin.Key}': {string.Join("; ", plugin.Select(error => error.Message))}");
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
                app.Messenger.ReportDiagnostics(result.Diagnostics);
            }

            return ExitCodes.Error;
        }

        // The run completed; the checks are the deliverable. Render them, then map their severity to an exit code.
        var report = result.Require();
        if (report.Checks.Count > 0)
        {
            app.Messenger.ReportDiagnostics(report.Checks);
        }

        if (report.HasErrors)
        {
            return ExitCodes.Error;
        }

        app.Messenger.Success($"All checks passed.");
        return ExitCodes.NoChanges;
    }
}
