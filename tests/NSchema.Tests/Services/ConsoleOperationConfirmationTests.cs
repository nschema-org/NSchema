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
    public async Task Confirm_Throws_WhenUserTypesAnythingElse()
    {
        // Arrange — declining at the prompt is a non-zero exit, so a wrapping script can't mistake "no" for success.
        var request = new ApplyConfirmationRequest(new MigrationPlan([], [], []));
        _console.Interactive();
        _console.Input.PushTextWithEnter("no");
        var sut = new ConsoleOperationConfirmation(autoApprove: false, _console);

        // Act / Assert
        await Should.ThrowAsync<ConfirmationDeclinedException>(async () =>
            await sut.Confirm(request, TestContext.Current.CancellationToken));
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
    public async Task Confirm_ThrowsHintingTheForceFlag_WhenForceUnlockIsNotInteractive()
    {
        // Arrange — force-unlock skips its prompt with --force, not --auto-approve, so the non-interactive error
        // names the right flag.
        var request = new ForceUnlockConfirmationRequest();
        var sut = new ConsoleOperationConfirmation(autoApprove: false, _console);

        // Act / Assert
        var ex = await Should.ThrowAsync<ConfirmationDeclinedException>(async () =>
            await sut.Confirm(request, TestContext.Current.CancellationToken));
        ex.Message.ShouldContain("--force");
    }

    [Fact]
    public async Task Confirm_Throws_WhenNotInteractive()
    {
        // Arrange — a non-interactive console (redirected stdin / CI / a container) has no input to read. Declining
        // silently would exit 0 and look like a successful no-op, so it must fail loudly instead.
        var request = new ApplyConfirmationRequest(new MigrationPlan([], [], []));
        var sut = new ConsoleOperationConfirmation(autoApprove: false, _console);

        // Act / Assert
        var ex = await Should.ThrowAsync<ConfirmationDeclinedException>(async () =>
            await sut.Confirm(request, TestContext.Current.CancellationToken));
        ex.Message.ShouldContain("no interactive terminal");
        ex.Message.ShouldContain("--auto-approve");
    }
}
