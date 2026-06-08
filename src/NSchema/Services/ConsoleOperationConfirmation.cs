using NSchema.Operations.Confirmation;
using Spectre.Console;

namespace NSchema.Services;

/// <summary>
/// An <see cref="IOperationConfirmation"/> that prompts on the terminal before changes are made to the database.
/// </summary>
internal sealed class ConsoleOperationConfirmation(bool autoApprove, IAnsiConsole console) : IOperationConfirmation
{
    public ValueTask<bool> Confirm(OperationConfirmationRequest request, CancellationToken cancellationToken = default)
    {
        // A destructive request (teardown) drops managed objects, so warn more loudly and prompt accordingly.
        if (request.IsDestructive)
        {
            console.MarkupLineInterpolated($"[red]NSchema will DROP managed objects via [yellow]{request.Plan.Actions.Count}[/] action(s). This is destructive and cannot be undone.[/]");
        }
        else
        {
            console.MarkupLineInterpolated($"NSchema will execute [yellow]{request.Plan.Actions.Count}[/] action(s) against the database.");
        }

        if (autoApprove)
        {
            console.MarkupLine("[grey]Auto-approve is enabled; skipping confirmation.[/]");
            return ValueTask.FromResult(true);
        }

        // Without an interactive terminal (redirected stdin / CI) there is nothing to read; decline rather than
        // block or throw, preserving the previous Console.ReadLine() behavior where EOF declined.
        if (!console.Profile.Capabilities.Interactive)
        {
            console.MarkupLine("[grey]No interactive terminal; declining. Use --auto-approve to proceed non-interactively.[/]");
            return ValueTask.FromResult(false);
        }

        var question = request.IsDestructive
            ? "Do you want to destroy these objects? Only [green]yes[/] will be accepted:"
            : "Do you want to apply these changes? Only [green]yes[/] will be accepted:";

        var response = console.Prompt(
            new TextPrompt<string>(question)
                .AllowEmpty());

        var approved = string.Equals(response.Trim(), "yes", StringComparison.OrdinalIgnoreCase);
        return ValueTask.FromResult(approved);
    }
}
