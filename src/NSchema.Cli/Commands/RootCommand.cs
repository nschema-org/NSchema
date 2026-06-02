using NSchema.Cli.Configuration;

namespace NSchema.Cli.Commands;

internal static class RootCommand
{
    public static System.CommandLine.RootCommand Create()
    {
        var root = new System.CommandLine.RootCommand("A declarative database schema migration tool.");

        root.Options.Add(CliOptions.Config);
        root.Options.Add(CliOptions.ConnectionString);
        root.Options.Add(CliOptions.Provider);
        root.Options.Add(CliOptions.Scope);
        root.Options.Add(CliOptions.Destructive);
        root.Options.Add(CliOptions.StateFile);

        root.Subcommands.Add(PlanCommand.Create());
        root.Subcommands.Add(ApplyCommand.Create());
        root.Subcommands.Add(RefreshCommand.Create());

        return root;
    }
}
