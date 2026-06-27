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
        var (summary, question, skipFlag) = Describe(request);
        ConsoleConfirmationPrompt.Require(console, autoApprove, summary, question, skipFlag);
        return ValueTask.FromResult(true);
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
            _ => throw new ArgumentOutOfRangeException(nameof(request), request, "Unknown confirmation request."),
        };
}
