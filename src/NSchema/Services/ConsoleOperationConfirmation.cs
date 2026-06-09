using NSchema.Operations.Confirmation;
using Spectre.Console;

namespace NSchema.Services;

/// <summary>
/// An <see cref="IOperationConfirmation"/> that prompts on the terminal before an operation makes irreversible changes.
/// </summary>
internal sealed class ConsoleOperationConfirmation(bool autoApprove, IAnsiConsole console) : IOperationConfirmation
{
    public ValueTask<bool> Confirm(OperationConfirmationRequest request, CancellationToken cancellationToken = default)
    {
        // The base request no longer carries a plan: apply/destroy describe an action count, force-unlock describes a
        // lock override, so each operation supplies its own summary, prompt, and the flag that skips it.
        var (summary, question, skipFlag) = Describe(request);

        console.MarkupLine(summary);

        if (autoApprove)
        {
            console.MarkupLine("[grey]Auto-approve is enabled; skipping confirmation.[/]");
            return ValueTask.FromResult(true);
        }

        // Without an interactive terminal (redirected stdin / CI) there is nothing to read; decline rather than
        // block or throw, preserving the previous Console.ReadLine() behavior where EOF declined.
        if (!console.Profile.Capabilities.Interactive)
        {
            console.MarkupLineInterpolated($"[grey]No interactive terminal; declining. Use {skipFlag} to proceed non-interactively.[/]");
            return ValueTask.FromResult(false);
        }

        var response = console.Prompt(new TextPrompt<string>(question).AllowEmpty());

        var approved = string.Equals(response.Trim(), "yes", StringComparison.OrdinalIgnoreCase);
        return ValueTask.FromResult(approved);
    }

    private static (string Summary, string Question, string SkipFlag) Describe(OperationConfirmationRequest request) =>
        request switch
        {
            ApplyConfirmationRequest apply => (
                $"NSchema will execute [yellow]{apply.Plan.Actions.Count}[/] action(s) against the database.",
                "Do you want to apply these changes? Only [green]yes[/] will be accepted:",
                "--auto-approve"),
            DestroyConfirmationRequest destroy => (
                $"[red]NSchema will DROP managed objects via [yellow]{destroy.Plan.Actions.Count}[/] action(s). This is destructive and cannot be undone.[/]",
                "Do you want to destroy these objects? Only [green]yes[/] will be accepted:",
                "--auto-approve"),
            ForceUnlockConfirmationRequest => (
                "[red]NSchema will forcibly release the state lock, even if another operation still holds it. This can corrupt the shared state.[/]",
                "Do you want to force-unlock the state? Only [green]yes[/] will be accepted:",
                "--force"),
            _ => throw new ArgumentOutOfRangeException(nameof(request), request, "Unknown confirmation request."),
        };
}
