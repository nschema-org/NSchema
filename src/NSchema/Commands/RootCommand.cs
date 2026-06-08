using NSchema.Commands.Apply;
using NSchema.Commands.Destroy;
using NSchema.Commands.Import;
using NSchema.Commands.Init;
using NSchema.Commands.Plan;
using NSchema.Commands.Refresh;
using NSchema.Commands.Validate;
using NSchema.Configuration;

namespace NSchema.Commands;

internal static class RootCommand
{
    public static System.CommandLine.RootCommand Create()
    {
        var root = new System.CommandLine.RootCommand("A declarative database schema migration tool.");

        root.Options.Add(CommonOptions.NoColor.Option);
        root.Options.Add(CommonOptions.Directory.Option);

        root.Subcommands.Add(InitCommand.Create());
        root.Subcommands.Add(ValidateCommand.Create());
        root.Subcommands.Add(PlanCommand.Create());
        root.Subcommands.Add(ApplyCommand.Create());
        root.Subcommands.Add(RefreshCommand.Create());
        root.Subcommands.Add(ImportCommand.Create());
        root.Subcommands.Add(DestroyCommand.Create());

        return root;
    }
}
