using System.CommandLine;
using NSchema.Commands.Db.Show;

namespace NSchema.Commands.Db;

/// <summary>
/// The <c>db</c> command group: inspect the live database directly through the provider.
/// </summary>
internal static class DbCommand
{
    public static Command Create()
    {
        var command = new Command("db", "Inspect the live database.");

        command.Subcommands.Add(DbShowCommand.Create());

        return command;
    }
}
