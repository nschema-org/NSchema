using NSchema.Operations.Confirmation;
using Spectre.Console;

namespace NSchema.Services;

/// <summary>
/// An <see cref="IOperationConfirmation"/> that prompts on the terminal before applying changes.
/// </summary>
internal sealed class ConsoleOperationConfirmation(bool autoApprove, IAnsiConsole console) : IOperationConfirmation
{
    public ValueTask<bool> Confirm(OperationConfirmationRequest request, CancellationToken cancellationToken = default)
    {
        console.MarkupLineInterpolated($"NSchema will execute [yellow]{request.Plan.Actions.Count}[/] action(s) against the database.");

        if (autoApprove)
        {
            console.MarkupLine("[grey]Auto-approve is enabled; skipping confirmation.[/]");
            return ValueTask.FromResult(true);
        }

        // Without an interactive terminal (redirected stdin / CI) there is nothing to read; decline rather than
        // block or throw, preserving the previous Console.ReadLine() behavior where EOF declined.
        if (!console.Profile.Capabilities.Interactive)
        {
            console.MarkupLine("[grey]No interactive terminal; declining. Use --auto-approve to apply non-interactively.[/]");
            return ValueTask.FromResult(false);
        }

        var response = console.Prompt(
            new TextPrompt<string>("Do you want to apply these changes? Only [green]yes[/] will be accepted:")
                .AllowEmpty());

        var approved = string.Equals(response.Trim(), "yes", StringComparison.OrdinalIgnoreCase);
        return ValueTask.FromResult(approved);
    }
}
