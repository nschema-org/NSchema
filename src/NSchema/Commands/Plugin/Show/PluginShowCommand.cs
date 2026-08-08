using System.CommandLine;
using NSchema.Configuration;
using NSchema.Configuration.Plugins;

namespace NSchema.Commands.Plugin.Show;

internal static class PluginShowCommand
{
    internal static readonly Argument<string> _labelArgument = new("label")
    {
        Description = "The label of the plugin to show, as declared by its PLUGIN statement (e.g. postgres, s3).",
    };

    public static Command Create()
    {
        var command = new Command("show", "Show the detail of one of the project's plugins, including its cache status.");
        command.Arguments.Add(_labelArgument);
        command.SetAction(Run);
        return command;
    }

    private static async Task<int> Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var reporter = ReporterFactory.CreateReporter(parseResult);
        var environment = ConfigurationFactory.ResolveEnvironment(parseResult);

        var resolved = await ConfigurationFactory.Load<PluginShowConfiguration>(parseResult, environment, cancellationToken);
        if (resolved.ReportFailure(reporter))
        {
            return ExitCodes.Error;
        }

        var configuration = resolved.Require();
        var plugins = PluginInventory.ForProject(configuration.Database, configuration.State, new PluginCache());
        var match = plugins.FirstOrDefault(p => p.Label == configuration.Label);
        if (match is null)
        {
            var configured = plugins.Count == 0 ? "none are configured" : string.Join(", ", plugins.Select(p => p.Label));
            reporter.ReportDiagnostics([
                PluginDiagnostics.NotConfigured(configuration.Label, configured)
            ]);
            return ExitCodes.Error;
        }

        reporter.ReportPluginDetail(match);
        return ExitCodes.NoChanges;
    }
}
