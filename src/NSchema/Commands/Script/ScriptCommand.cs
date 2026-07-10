using System.CommandLine;
using NSchema.Commands.Script.List;
using NSchema.Commands.Script.Taint;
using NSchema.Commands.Script.Untaint;

namespace NSchema.Commands.Script;

/// <summary>
/// The <c>script</c> command group: inspect and manage the script executions recorded in the state.
/// </summary>
internal static class ScriptCommand
{
    public static Command Create()
    {
        var command = new Command("script", "Manage the script executions recorded in the state.");

        command.Subcommands.Add(ScriptListCommand.Create());
        command.Subcommands.Add(ScriptTaintCommand.Create());
        command.Subcommands.Add(ScriptUntaintCommand.Create());

        return command;
    }
}
