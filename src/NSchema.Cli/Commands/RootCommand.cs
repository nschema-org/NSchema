using System.Reflection;

namespace NSchema.Cli.Commands;

internal static class RootCommand
{
    public static System.CommandLine.RootCommand Create()
    {
        var root = new System.CommandLine.RootCommand("A declarative database schema migration tool.");
        SetName(root, "nschema");

        root.Subcommands.Add(InitCommand.Create());
        root.Subcommands.Add(PlanCommand.Create());
        root.Subcommands.Add(ApplyCommand.Create());
        root.Subcommands.Add(RefreshCommand.Create());

        return root;
    }

    // System.CommandLine derives the root command's name from the executable ("NSchema.Cli") and exposes no API to
    // override it, so help/usage would read "NSchema.Cli" instead of "nschema". We can't rename the assembly to
    // "nschema" — that collides with the core NSchema assembly — so we set the backing field directly. The
    // null-conditional degrades to the default name rather than throwing if this internal ever changes, and
    // RootCommandTests.HasTheNschemaCommandName guards it so the breakage surfaces in CI.
    private static void SetName(System.CommandLine.Symbol symbol, string name)
        => typeof(System.CommandLine.Symbol)
            .GetField("<Name>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)
            ?.SetValue(symbol, name);
}
