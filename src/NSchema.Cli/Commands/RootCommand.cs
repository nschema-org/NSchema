using NSchema.Cli.Configuration;

namespace NSchema.Cli.Commands;

internal static class RootCommand
{
    public static System.CommandLine.RootCommand Create()
    {
        var root = new System.CommandLine.RootCommand("A declarative database schema migration tool.");
        foreach (var option in CliOptions.Global)
        {
            root.Options.Add(option);
        }

        root.Subcommands.Add(PlanCommand.Create());
        root.Subcommands.Add(ApplyCommand.Create());
        root.Subcommands.Add(RefreshCommand.Create());

        return root;
    }
}
