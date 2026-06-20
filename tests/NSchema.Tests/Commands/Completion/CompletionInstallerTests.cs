using NSchema.Commands.Completion;

namespace NSchema.Tests.Commands.Completion;

public sealed class CompletionInstallerTests
{
    private const string SourceLine = "source <(nschema completion bash)";

    [Fact]
    public void ApplyBlock_EmptyFile_WritesJustTheBlock()
    {
        // Act
        var result = CompletionInstaller.ApplyBlock("", SourceLine);

        // Assert
        result.ShouldBe(
            $"{CompletionInstaller.BeginMarker}\n{SourceLine}\n{CompletionInstaller.EndMarker}\n");
    }

    [Fact]
    public void ApplyBlock_ExistingContent_AppendsBlockAfterABlankLine()
    {
        // Arrange
        const string existing = "export PATH=$PATH:/usr/local/bin\n";

        // Act
        var result = CompletionInstaller.ApplyBlock(existing, SourceLine);

        // Assert — original content is preserved, then a blank line, then the managed block.
        result.ShouldBe(
            existing + "\n" +
            $"{CompletionInstaller.BeginMarker}\n{SourceLine}\n{CompletionInstaller.EndMarker}\n");
    }

    [Fact]
    public void ApplyBlock_IsIdempotent()
    {
        // Arrange
        var once = CompletionInstaller.ApplyBlock("alias ll='ls -la'\n", SourceLine);

        // Act
        var twice = CompletionInstaller.ApplyBlock(once, SourceLine);

        // Assert
        twice.ShouldBe(once);
    }

    [Fact]
    public void ApplyBlock_ReplacesAnExistingBlockRatherThanDuplicating()
    {
        // Arrange — a stale block (e.g. from an old version) should be rewritten in place.
        var stale = CompletionInstaller.ApplyBlock("", "source <(nschema completion zsh)");

        // Act
        var result = CompletionInstaller.ApplyBlock(stale, SourceLine);

        // Assert
        result.ShouldContain(SourceLine);
        result.ShouldNotContain("zsh");
        result.Split(CompletionInstaller.BeginMarker).Length.ShouldBe(2); // exactly one block
    }

    [Fact]
    public void RemoveBlock_DeletesTheBlockAndTheBlankLineWeAdded()
    {
        // Arrange
        const string original = "export EDITOR=vim\n";
        var withBlock = CompletionInstaller.ApplyBlock(original, SourceLine);

        // Act
        var result = CompletionInstaller.RemoveBlock(withBlock);

        // Assert
        result.ShouldBe(original);
    }

    [Fact]
    public void RemoveBlock_WithoutABlock_LeavesContentUntouched()
    {
        // Arrange
        const string original = "export EDITOR=vim\n";

        // Act / Assert
        CompletionInstaller.RemoveBlock(original).ShouldBe(original);
    }

    [Theory]
    [InlineData("bash")]
    [InlineData("zsh")]
    [InlineData("fish")]
    [InlineData("pwsh")]
    public void TargetFor_KnownShell_ResolvesAPathAndSourceLine(string shell)
    {
        // Act
        var (path, sourceLine) = CompletionInstaller.TargetFor(shell);

        // Assert
        path.ShouldNotBeNullOrWhiteSpace();
        sourceLine.ShouldContain($"completion {shell}");
    }

    [Fact]
    public async Task InstallAt_ThenUninstallAt_RoundTripsTheFile()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var path = Path.Combine(Path.GetTempPath(), $"nschema-rc-{Guid.NewGuid():N}");
        await File.WriteAllTextAsync(path, "export EDITOR=vim\n", cancellationToken);
        try
        {
            // Act
            var installed = await CompletionInstaller.InstallAt(path, SourceLine, cancellationToken);
            var afterInstall = await File.ReadAllTextAsync(path, cancellationToken);
            var removed = await CompletionInstaller.UninstallAt(path, cancellationToken);
            var afterUninstall = await File.ReadAllTextAsync(path, cancellationToken);

            // Assert
            installed.Changed.ShouldBeTrue();
            afterInstall.ShouldContain(SourceLine);

            removed.Changed.ShouldBeTrue();
            afterUninstall.ShouldBe("export EDITOR=vim\n");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task InstallAt_AlreadyInstalled_ReportsNoChange()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var path = Path.Combine(Path.GetTempPath(), $"nschema-rc-{Guid.NewGuid():N}");
        try
        {
            await CompletionInstaller.InstallAt(path, SourceLine, cancellationToken);

            // Act
            var second = await CompletionInstaller.InstallAt(path, SourceLine, cancellationToken);

            // Assert
            second.Changed.ShouldBeFalse();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task UninstallAt_MissingFile_ReportsNoChange()
    {
        // Arrange — a path that does not exist.
        var path = Path.Combine(Path.GetTempPath(), $"nschema-rc-{Guid.NewGuid():N}");

        // Act
        var result = await CompletionInstaller.UninstallAt(path, TestContext.Current.CancellationToken);

        // Assert
        result.Changed.ShouldBeFalse();
        File.Exists(path).ShouldBeFalse();
    }
}
