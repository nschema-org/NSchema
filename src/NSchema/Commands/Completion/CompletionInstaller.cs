namespace NSchema.Commands.Completion;

/// <summary>
/// Installs and removes the shell completion shim by managing a delimited block in the shell's
/// startup file (<c>~/.bashrc</c>, <c>~/.zshrc</c>, fish's <c>config.fish</c>, or the PowerShell
/// <c>$PROFILE</c>). The block <em>sources</em> <c>nschema completion &lt;shell&gt;</c> rather than
/// embedding the script, so the completions always track the installed binary, and uninstall removes
/// exactly the block we wrote.
/// </summary>
internal static class CompletionInstaller
{
    internal const string BeginMarker = "# >>> nschema completion >>>";
    internal const string EndMarker = "# <<< nschema completion <<<";

    /// <summary>The result of an install/uninstall: the file touched and whether its contents changed.</summary>
    internal readonly record struct InstallOutcome(string Path, bool Changed);

    /// <summary>Installs the completion block into <paramref name="shell"/>'s startup file.</summary>
    public static Task<InstallOutcome> Install(string shell, CancellationToken cancellationToken = default)
    {
        var (path, sourceLine) = TargetFor(shell);
        return InstallAt(path, sourceLine, cancellationToken);
    }

    /// <summary>Removes the completion block from <paramref name="shell"/>'s startup file.</summary>
    public static Task<InstallOutcome> Uninstall(string shell, CancellationToken cancellationToken = default)
    {
        var (path, _) = TargetFor(shell);
        return UninstallAt(path, cancellationToken);
    }

    internal static async Task<InstallOutcome> InstallAt(string path, string sourceLine, CancellationToken cancellationToken = default)
    {
        var existing = File.Exists(path) ? await File.ReadAllTextAsync(path, cancellationToken) : "";
        var updated = ApplyBlock(existing, sourceLine);
        if (updated == existing)
        {
            return new InstallOutcome(path, Changed: false);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, updated, cancellationToken);
        return new InstallOutcome(path, Changed: true);
    }

    internal static async Task<InstallOutcome> UninstallAt(string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
        {
            return new InstallOutcome(path, Changed: false);
        }

        var existing = await File.ReadAllTextAsync(path, cancellationToken);
        var updated = RemoveBlock(existing);
        if (updated == existing)
        {
            return new InstallOutcome(path, Changed: false);
        }

        await File.WriteAllTextAsync(path, updated, cancellationToken);
        return new InstallOutcome(path, Changed: true);
    }

    /// <summary>
    /// Inserts the managed block into <paramref name="contents"/>, replacing any block already present so the
    /// operation is idempotent. A blank line separates the block from preceding content.
    /// </summary>
    internal static string ApplyBlock(string contents, string sourceLine)
    {
        var block = $"{BeginMarker}\n{sourceLine}\n{EndMarker}\n";

        if (TryFindBlock(contents, out var start, out var end))
        {
            return contents[..start] + block + contents[end..];
        }

        if (contents.Length == 0)
        {
            return block;
        }

        var separator = contents.EndsWith('\n') ? "\n" : "\n\n";
        return contents + separator + block;
    }

    /// <summary>Removes the managed block from <paramref name="contents"/>, including the blank line we added before it.</summary>
    internal static string RemoveBlock(string contents)
    {
        if (!TryFindBlock(contents, out var start, out var end))
        {
            return contents;
        }

        var before = contents[..start];
        if (before.EndsWith("\n\n", StringComparison.Ordinal))
        {
            before = before[..^1];
        }

        return before + contents[end..];
    }

    /// <summary>Locates the managed block, reporting the index of <see cref="BeginMarker"/> and one past the block's trailing newline.</summary>
    private static bool TryFindBlock(string contents, out int start, out int end)
    {
        start = contents.IndexOf(BeginMarker, StringComparison.Ordinal);
        end = -1;
        if (start < 0)
        {
            return false;
        }

        var marker = contents.IndexOf(EndMarker, start, StringComparison.Ordinal);
        if (marker < 0)
        {
            start = -1;
            return false;
        }

        end = marker + EndMarker.Length;
        if (end < contents.Length && contents[end] == '\n')
        {
            end++;
        }

        return true;
    }

    /// <summary>Maps a shell to its startup file and the line that loads completion into a new session.</summary>
    internal static (string Path, string SourceLine) TargetFor(string shell)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return shell switch
        {
            "bash" => (Path.Combine(home, ".bashrc"), "source <(nschema completion bash)"),
            "zsh" => (Path.Combine(home, ".zshrc"), "source <(nschema completion zsh)"),
            "fish" => (Path.Combine(home, ".config", "fish", "config.fish"), "nschema completion fish | source"),
            "pwsh" => (PwshProfile(home), "nschema completion pwsh | Out-String | Invoke-Expression"),
            _ => throw new ArgumentOutOfRangeException(nameof(shell), shell, "Unknown shell."),
        };
    }

    // PowerShell 7+'s current-user profile lives under Documents on Windows and ~/.config/powershell elsewhere.
    private static string PwshProfile(string home) => OperatingSystem.IsWindows()
        ? Path.Combine(home, "Documents", "PowerShell", "Microsoft.PowerShell_profile.ps1")
        : Path.Combine(home, ".config", "powershell", "Microsoft.PowerShell_profile.ps1");
}
