using NSchema.Plan.Model;
using NSchema.Services;
using Spectre.Console.Testing;

namespace NSchema.Tests.Services;

public sealed class ConsoleMigrationConfirmationTests
{
    private readonly TestConsole _console = new();

    public ConsoleMigrationConfirmationTests() => _console.Profile.Width = 200;

    [Fact]
    public async Task Confirm_ReturnsTrue_WhenAutoApprove()
    {
        // Arrange
        var sut = new ConsoleMigrationConfirmation(autoApprove: true, _console);

        // Act
        var approved = await sut.Confirm(new MigrationPlan([]), TestContext.Current.CancellationToken);

        // Assert
        approved.ShouldBeTrue();
        _console.Output.ShouldContain("Auto-approve");
    }

    [Fact]
    public async Task Confirm_ReturnsTrue_WhenUserTypesYes()
    {
        // Arrange
        _console.Interactive();
        _console.Input.PushTextWithEnter("yes");
        var sut = new ConsoleMigrationConfirmation(autoApprove: false, _console);

        // Act
        var approved = await sut.Confirm(new MigrationPlan([]), TestContext.Current.CancellationToken);

        // Assert
        approved.ShouldBeTrue();
    }

    [Fact]
    public async Task Confirm_ReturnsFalse_WhenUserTypesAnythingElse()
    {
        // Arrange
        _console.Interactive();
        _console.Input.PushTextWithEnter("no");
        var sut = new ConsoleMigrationConfirmation(autoApprove: false, _console);

        // Act
        var approved = await sut.Confirm(new MigrationPlan([]), TestContext.Current.CancellationToken);

        // Assert
        approved.ShouldBeFalse();
    }

    [Fact]
    public async Task Confirm_DeclinesWithoutPrompting_WhenNotInteractive()
    {
        // Arrange — a non-interactive console (redirected stdin / CI) has no input to read.
        var sut = new ConsoleMigrationConfirmation(autoApprove: false, _console);

        // Act
        var approved = await sut.Confirm(new MigrationPlan([]), TestContext.Current.CancellationToken);

        // Assert
        approved.ShouldBeFalse();
        _console.Output.ShouldContain("No interactive terminal");
    }
}
