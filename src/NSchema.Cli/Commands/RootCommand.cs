using NSchema.Cli.Configuration;

namespace NSchema.Cli.Commands;

internal static class RootCommand
{
    public static System.CommandLine.RootCommand Create()
    {
        var root = new System.CommandLine.RootCommand("A declarative database schema migration tool.");

        root.Options.Add(CliOptions.Global.Config);
        root.Options.Add(CliOptions.Database.Provider);
        root.Options.Add(CliOptions.Database.ConnectionString);
        root.Options.Add(CliOptions.State.Type);
        root.Options.Add(CliOptions.State.ConnectionString);
        root.Options.Add(CliOptions.State.File);

        root.Subcommands.Add(InitCommand.Create());
        root.Subcommands.Add(PlanCommand.Create());
        root.Subcommands.Add(ApplyCommand.Create());
        root.Subcommands.Add(RefreshCommand.Create());

        return root;
    }
}
