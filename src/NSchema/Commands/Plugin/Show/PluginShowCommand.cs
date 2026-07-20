using System.CommandLine;
using NSchema.Configuration;
using NSchema.Configuration.Plugins;
using NSchema.Services.Reporting;

namespace NSchema.Commands.Plugin.Show;

internal static class PluginShowCommand
{
    internal static readonly Argument<string> LabelArgument = new("label")
    {
        Description = "The label of the plugin to show, as declared by its PLUGIN statement (e.g. postgres, s3).",
    };

    public static Command Create()
    {
        var command = new Command("show", "Show the detail of one of the project's plugins, including its cache status.");
        command.Arguments.Add(LabelArgument);
        command.SetAction(Run);
        return command;
    }

    private static async Task Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var environment = ConfigurationFactory.ResolveEnvironment(parseResult);
        var configuration = await ConfigurationFactory.Load<PluginShowConfiguration>(parseResult, environment, cancellationToken);

        var plugins = PluginInventory.ForProject(configuration.Provider, configuration.State, new PluginCache());
        var match = plugins.FirstOrDefault(p => p.Label.Equals(configuration.Label, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            var configured = plugins.Count == 0 ? "none are configured" : string.Join(", ", plugins.Select(p => p.Label));
            throw new InvalidOperationException(
                $"No plugin labelled '{configuration.Label}' is configured for this project (configured: {configured}).");
        }

        ReporterFactory.CreateMessenger(parseResult).ReportPluginDetail(match);
    }
}
