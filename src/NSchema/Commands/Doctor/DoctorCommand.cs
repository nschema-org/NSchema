using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using NSchema.Configuration;
using NSchema.Configuration.Plugins;
using NSchema.Operations;
using NSchema.Operations.Doctor;
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

    private static async Task Run(ParseResult parseResult, CancellationToken cancellationToken)
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

        app.Services.GetRequiredService<IConsolePresenter>().ReportEnvironment(environment);

        if (diagnostics.Count > 0)
        {
            // The plugins didn't fully wire up, so the live database/state checks can't run meaningfully — report every
            // plugin problem and stop, rather than following them with misleading "not configured" lines.
            var reporter = app.Services.GetRequiredService<IOperationReporter>();
            foreach (var diagnostic in diagnostics)
            {
                reporter.Warn($"Plugin '{diagnostic.Label}': {string.Join("; ", diagnostic.Errors)}");
            }

            throw new InvalidOperationException(
                $"Diagnostics found {diagnostics.Count} plugin problem{(diagnostics.Count == 1 ? "" : "s")}. See the messages above.");
        }

        await app.Doctor(new DoctorArguments(), cancellationToken);
    }
}
