using System.CommandLine;
using NSchema.Configuration;
using NSchema.Configuration.Plugins;

namespace NSchema.Commands.Init;

internal static class InitCommand
{
    public static Command Create()
    {
        var command = new Command("init", "Restore the provider and backend plugins pinned in the project configuration, locking declared ranges.");
        command.SetAction(Run);
        return command;
    }

    private static async Task<int> Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var environment = ConfigurationFactory.ResolveEnvironment(parseResult);
        ConfigurationFactory.ApplyWorkingDirectory(parseResult);
        var root = Directory.GetCurrentDirectory();

        // An unreadable .editorconfig fails the build, and requiring a value it does not carry would crash the
        // command with an internal error rather than saying what is wrong with the file.
        var built = CliApplicationBuilder.Create(parseResult).Build();
        if (built.ReportFailure(ReporterFactory.CreateReporter(parseResult)))
        {
            return ExitCodes.Error;
        }

        using var app = built.Require();

        var initialized = await ProjectInitializer.Initialize(root, environment, new PluginLoader(root), app.Reporter, cancellationToken);
        return initialized.ReportFailure(app.Reporter) ? ExitCodes.Error : ExitCodes.NoChanges;
    }
}
