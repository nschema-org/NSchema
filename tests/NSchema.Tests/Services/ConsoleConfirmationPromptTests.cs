using NSchema.Services;
using Spectre.Console.Testing;

namespace NSchema.Tests.Services;

public sealed class ConsoleConfirmationPromptTests
{
    private const string Summary = "NSchema will execute 3 statement(s) against the database.";
    private const string Question = "Do you want to apply these changes? Only yes will be accepted:";

    private readonly TestConsole _console = new();

    public ConsoleConfirmationPromptTests() => _console.Profile.Width = 200;

    private void Require(bool autoApprove) =>
        ConsoleConfirmationPrompt.Require(_console, autoApprove, Summary, Question, "--auto-approve");

    [Fact]
    public void Require_Proceeds_WhenAutoApprove()
    {
        // Act
        Should.NotThrow(() => Require(autoApprove: true));

        // Assert — the summary is shown and the prompt is skipped.
        _console.Output.ShouldContain("Auto-approve");
    }

    [Fact]
    public void Require_Proceeds_WhenUserTypesYes()
    {
        // Arrange
        _console.Interactive();
        _console.Input.PushTextWithEnter("yes");

        // Act / Assert
        Should.NotThrow(() => Require(autoApprove: false));
    }

    [Fact]
    public void Require_Throws_WhenUserTypesAnythingElse()
    {
        // Arrange — declining at the prompt is a non-zero exit, so a wrapping script can't mistake "no" for success.
        _console.Interactive();
        _console.Input.PushTextWithEnter("no");

        // Act / Assert
        Should.Throw<ConfirmationDeclinedException>(() => Require(autoApprove: false));
    }

    [Fact]
    public void Require_PresentsTheSummary_BeforePrompting()
    {
        // Arrange
        _console.Interactive();
        _console.Input.PushTextWithEnter("yes");

        // Act
        Require(autoApprove: false);

        // Assert
        _console.Output.ShouldContain("execute 3 statement(s)");
    }

    [Fact]
    public void Require_Throws_WhenNotInteractive()
    {
        // Arrange — a non-interactive console (redirected stdin / CI / a container) has no input to read. Declining
        // silently would exit 0 and look like a successful no-op, so it must fail loudly instead.

        // Act / Assert
        var ex = Should.Throw<ConfirmationDeclinedException>(() => Require(autoApprove: false));
        ex.Message.ShouldContain("no interactive terminal");
        ex.Message.ShouldContain("--auto-approve");
    }
}
