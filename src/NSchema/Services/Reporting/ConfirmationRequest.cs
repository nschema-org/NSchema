namespace NSchema.Services.Reporting;

/// <summary>
/// What a command asks the operator to approve before a dangerous action proceeds.
/// </summary>
internal sealed class ConfirmationRequest(ConsoleMessage summary)
{
    /// <summary>
    /// The Spectre markup of the summary describing what would happen.
    /// </summary>
    public string SummaryMarkup { get; } = summary.Styled;

    /// <summary>
    /// The plain text of the summary describing what would happen.
    /// </summary>
    public string SummaryText { get; } = summary.Plain;

    /// <summary>
    /// The question to put to the operator ("Do you want to apply these changes?").
    /// </summary>
    public required string Question { get; init; }

    /// <summary>
    /// The flag that skips the prompt, named when there is no terminal to ask on ("--auto-approve").
    /// </summary>
    public required string SkipFlag { get; init; }

    /// <summary>
    /// Whether the operator pre-approved on the command line, reducing the confirmation to narration.
    /// </summary>
    public required bool AutoApprove { get; init; }

    /// <summary>
    /// Whether the action destroys something irrecoverably, warranting the danger styling.
    /// </summary>
    public bool Destructive { get; init; }
}
