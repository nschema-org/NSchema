using System.CommandLine;
using NSchema.Commands.State.Show;

namespace NSchema.Commands.State;

/// <summary>
/// The <c>state</c> command group: inspect (and, in future, manage — pull/push/move) the recorded schema state.
/// </summary>
internal static class StateCommand
{
    public static Command Create()
    {
        var command = new Command("state", "Inspect the recorded schema state.");

        command.Subcommands.Add(StateShowCommand.Create());

        return command;
    }
}
