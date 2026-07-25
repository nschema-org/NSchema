using System.Runtime.CompilerServices;

namespace NSchema.Tests.Architecture;

/// <summary>
/// Every file's namespace matches the folder it sits in. Not an ArchUnit rule — it is a fact about the source tree,
/// which compiled types no longer carry — so it reads the files directly.
/// </summary>
/// <remarks>
/// The invariant is what lets the layering rules be trusted from a file's location alone: if the two could drift,
/// "which slice is this in?" would have two answers.
/// </remarks>
public sealed class NamespaceLayoutTests
{
    [Fact]
    public void EveryFile_DeclaresTheNamespaceItsFolderImplies()
    {
        // Arrange
        var root = SourceRoot();
        var sources = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories).Where(IsSource);

        // Act
        var mismatches = sources
            .Select(file => (File: Path.GetRelativePath(root, file), Declared: DeclaredNamespace(file), Implied: ImpliedNamespace(root, file)))
            // A file with no namespace declares nothing to contradict (assembly-level attributes, for instance).
            .Where(entry => entry.Declared is not null && entry.Declared != entry.Implied)
            .Select(entry => $"{entry.File}: declares '{entry.Declared}', folder implies '{entry.Implied}'")
            .ToList();

        // Assert
        mismatches.ShouldBeEmpty();
    }

    private static bool IsSource(string file) =>
        !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
        && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}");

    private static string ImpliedNamespace(string root, string file)
    {
        var folder = Path.GetRelativePath(root, Path.GetDirectoryName(file)!);
        return folder == "." ? "NSchema" : "NSchema." + folder.Replace(Path.DirectorySeparatorChar, '.');
    }

    private static string? DeclaredNamespace(string file) => File.ReadLines(file)
        .FirstOrDefault(line => line.StartsWith("namespace ", StringComparison.Ordinal))
        ?["namespace ".Length..].TrimEnd(';', ' ');

    /// <summary>The engine's source directory, located from this file rather than the test binary's location.</summary>
    private static string SourceRoot([CallerFilePath] string thisFile = "")
    {
        // tests/NSchema.Core.Tests/Architecture/<this file>  ->  src/NSchema.Core
        var repository = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", ".."));
        return Path.Combine(repository, "src", "NSchema.Core");
    }
}
