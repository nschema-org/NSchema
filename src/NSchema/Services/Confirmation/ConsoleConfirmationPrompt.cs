using Spectre.Console;

namespace NSchema.Services.Confirmation;

/// <summary>
/// The shared terminal confirmation gate: present a summary and, unless auto-approved, require the operator to type "yes" before a dangerous action proceeds.
/// </summary>
internal static class ConsoleConfirmationPrompt
{
    /// <summary>
    /// Presents <paramref name="summary"/> and requires a "yes" before continuing, unless <paramref name="autoApprove"/>
    /// is set. Throws <see cref="ConfirmationDeclinedException"/> when the operator declines, or when there is no
    /// interactive terminal to prompt on (pointing the caller at <paramref name="skipFlag"/>).
    /// </summary>
    public static void Require(IAnsiConsole console, bool autoApprove, string summary, string question, string skipFlag)
    {
        console.MarkupLine(summary);

        if (autoApprove)
        {
            console.MarkupLine("[grey]Auto-approve is enabled; skipping confirmation.[/]");
            return;
        }

        // Without an interactive terminal (redirected stdin / CI / a container) there is nothing to read.
        if (!console.Profile.Capabilities.Interactive)
        {
            throw new ConfirmationDeclinedException($"This operation needs confirmation, but there is no interactive terminal. Re-run with {skipFlag} to proceed non-interactively.");
        }

        var response = console.Prompt(new TextPrompt<string>(question).AllowEmpty());

        // Any answer other than "yes" cancels.
        if (!string.Equals(response.Trim(), "yes", StringComparison.OrdinalIgnoreCase))
        {
            throw new ConfirmationDeclinedException("Cancelled by operator. No changes were made.");
        }
    }
}
