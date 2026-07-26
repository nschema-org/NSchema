using System.CommandLine;
using NSchema.Commands.Database.Show;

namespace NSchema.Commands.Database;

/// <summary>
/// The <c>db</c> command group: inspect the live database directly through the provider.
/// </summary>
internal static class DatabaseCommand
{
    public static Command Create()
    {
        var command = new Command("database", "Inspect the live database.");
        command.Subcommands.Add(DatabaseShowCommand.Create());

        return command;
    }
}
