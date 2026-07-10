using NSchema.Commands.Apply;
using NSchema.Commands.Completion;
using NSchema.Commands.Db;
using NSchema.Commands.Destroy;
using NSchema.Commands.Doctor;
using NSchema.Commands.Drift;
using NSchema.Commands.Fmt;
using NSchema.Commands.Import;
using NSchema.Commands.Init;
using NSchema.Commands.Lock;
using NSchema.Commands.Plan;
using NSchema.Commands.Plugin;
using NSchema.Commands.Refresh;
using NSchema.Commands.Scaffold;
using NSchema.Commands.Script;
using NSchema.Commands.State;
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
        root.Options.Add(CommonOptions.Environment.Option);
        root.Options.Add(CommonOptions.Json.Option);
        root.Options.Add(CommonOptions.Format.Option);
        root.Options.Add(CommonOptions.NoInit.Option);
        root.Options.Add(CommonOptions.Verbose.Option);
        root.Options.Add(CommonOptions.Quiet.Option);

        root.Subcommands.Add(InitCommand.Create());
        root.Subcommands.Add(ScaffoldCommand.Create());
        root.Subcommands.Add(ValidateCommand.Create());
        root.Subcommands.Add(FmtCommand.Create());
        root.Subcommands.Add(PlanCommand.Create());
        root.Subcommands.Add(ApplyCommand.Create());
        root.Subcommands.Add(RefreshCommand.Create());
        root.Subcommands.Add(ImportCommand.Create());
        root.Subcommands.Add(DestroyCommand.Create());
        root.Subcommands.Add(StateCommand.Create());
        root.Subcommands.Add(ScriptCommand.Create());
        root.Subcommands.Add(DbCommand.Create());
        root.Subcommands.Add(DriftCommand.Create());
        root.Subcommands.Add(DoctorCommand.Create());
        root.Subcommands.Add(LockCommand.Create());
        root.Subcommands.Add(PluginCommand.Create());
        root.Subcommands.Add(CompletionCommand.Create());

        // Backs `nschema [suggest:<pos>] "<command line>"`, which the shell-completion scripts call to compute
        // candidates for the current word. The `completion <shell>` command emits those scripts.
        root.Add(new System.CommandLine.Completions.SuggestDirective());

        return root;
    }
}
