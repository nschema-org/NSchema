using NSchema.Operations.Confirmation;
using NSchema.Plan.Model;
using NSchema.Services;
using Spectre.Console.Testing;

namespace NSchema.Tests.Services;

public sealed class ConsoleOperationConfirmationTests
{
    private readonly TestConsole _console = new();

    public ConsoleOperationConfirmationTests() => _console.Profile.Width = 200;

    [Fact]
    public async Task Confirm_ReturnsTrue_WhenAutoApprove()
    {
        // Arrange
        var request = new ApplyConfirmationRequest(new MigrationPlan([], [], []));
        var sut = new ConsoleOperationConfirmation(autoApprove: true, _console);

        // Act
        var approved = await sut.Confirm(request, TestContext.Current.CancellationToken);

        // Assert
        approved.ShouldBeTrue();
        _console.Output.ShouldContain("Auto-approve");
    }

    [Fact]
    public async Task Confirm_ReturnsTrue_WhenUserTypesYes()
    {
        // Arrange
        var request = new ApplyConfirmationRequest(new MigrationPlan([], [], []));
        _console.Interactive();
        _console.Input.PushTextWithEnter("yes");
        var sut = new ConsoleOperationConfirmation(autoApprove: false, _console);

        // Act
        var approved = await sut.Confirm(request, TestContext.Current.CancellationToken);

        // Assert
        approved.ShouldBeTrue();
    }

    [Fact]
    public async Task Confirm_ReturnsFalse_WhenUserTypesAnythingElse()
    {
        // Arrange
        var request = new ApplyConfirmationRequest(new MigrationPlan([], [], []));
        _console.Interactive();
        _console.Input.PushTextWithEnter("no");
        var sut = new ConsoleOperationConfirmation(autoApprove: false, _console);

        // Act
        var approved = await sut.Confirm(request, TestContext.Current.CancellationToken);

        // Assert
        approved.ShouldBeFalse();
    }

    [Fact]
    public async Task Confirm_WarnsAndPromptsToDestroy_WhenRequestIsDestructive()
    {
        // Arrange
        var request = new DestroyConfirmationRequest(new MigrationPlan([], [], []));
        _console.Interactive();
        _console.Input.PushTextWithEnter("yes");
        var sut = new ConsoleOperationConfirmation(autoApprove: false, _console);

        // Act
        var approved = await sut.Confirm(request, TestContext.Current.CancellationToken);

        // Assert
        approved.ShouldBeTrue();
        _console.Output.ShouldContain("DROP");
        _console.Output.ShouldContain("destroy these objects");
    }

    [Fact]
    public async Task Confirm_WarnsAndPromptsToForceUnlock_ForForceUnlockRequest()
    {
        // Arrange
        var request = new ForceUnlockConfirmationRequest();
        _console.Interactive();
        _console.Input.PushTextWithEnter("yes");
        var sut = new ConsoleOperationConfirmation(autoApprove: false, _console);

        // Act
        var approved = await sut.Confirm(request, TestContext.Current.CancellationToken);

        // Assert
        approved.ShouldBeTrue();
        _console.Output.ShouldContain("force-unlock the state");
    }

    [Fact]
    public async Task Confirm_HintsTheForceFlag_WhenForceUnlockIsNotInteractive()
    {
        // Arrange — force-unlock skips its prompt with --force, not --auto-approve, so the non-interactive hint
        // names the right flag.
        var request = new ForceUnlockConfirmationRequest();
        var sut = new ConsoleOperationConfirmation(autoApprove: false, _console);

        // Act
        var approved = await sut.Confirm(request, TestContext.Current.CancellationToken);

        // Assert
        approved.ShouldBeFalse();
        _console.Output.ShouldContain("--force");
    }

    [Fact]
    public async Task Confirm_DeclinesWithoutPrompting_WhenNotInteractive()
    {
        // Arrange — a non-interactive console (redirected stdin / CI) has no input to read.
        var request = new ApplyConfirmationRequest(new MigrationPlan([], [], []));
        var sut = new ConsoleOperationConfirmation(autoApprove: false, _console);

        // Act
        var approved = await sut.Confirm(request, TestContext.Current.CancellationToken);

        // Assert
        approved.ShouldBeFalse();
        _console.Output.ShouldContain("No interactive terminal");
    }
}
