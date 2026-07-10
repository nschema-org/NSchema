using System.CommandLine;
using NSchema.Commands.State.Pull;
using NSchema.Commands.State.Push;
using NSchema.Commands.State.Show;

namespace NSchema.Commands.State;

/// <summary>
/// The <c>state</c> command group: inspect and manage the recorded schema state.
/// </summary>
internal static class StateCommand
{
    public static Command Create()
    {
        var command = new Command("state", "Inspect and manage the recorded schema state.");

        command.Subcommands.Add(StateShowCommand.Create());
        command.Subcommands.Add(StatePullCommand.Create());
        command.Subcommands.Add(StatePushCommand.Create());

        return command;
    }
}
