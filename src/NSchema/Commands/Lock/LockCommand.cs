using System.CommandLine;
using NSchema.Commands.Lock.Acquire;
using NSchema.Commands.Lock.Release;
using NSchema.Commands.Lock.Status;

namespace NSchema.Commands.Lock;

/// <summary>
/// The <c>lock</c> command group.
/// </summary>
internal static class LockCommand
{
    public static Command Create()
    {
        var command = new Command("lock", "Inspect and manage the state lock.");

        command.Subcommands.Add(LockStatusCommand.Create());
        command.Subcommands.Add(LockAcquireCommand.Create());
        command.Subcommands.Add(LockReleaseCommand.Create());

        return command;
    }
}
