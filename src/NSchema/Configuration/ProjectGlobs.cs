using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;

namespace NSchema.Configuration;

/// <summary>
/// The glob patterns that classify a project's files by role.
/// </summary>
internal static class ProjectGlobs
{
    /// <summary>
    /// The extensions a project file may carry.
    /// </summary>
    public static readonly IReadOnlyList<string> Extensions = ["nsql", "sql"];

    /// <summary>
    /// Every project file, recursively — one pattern per extension.
    /// </summary>
    public static IEnumerable<string> AllProjectFiles => Extensions.Select(extension => $"**/*.{extension}");

    /// <summary>
    /// Any environment overlay file, for any environment.
    /// </summary>
    public static IEnumerable<string> AnyEnvironmentGlobs => Extensions.Select(extension => $"**/*.env.*.{extension}");

    /// <summary>
    /// The overlay globs for a single environment.
    /// </summary>
    public static IEnumerable<string> EnvironmentGlobs(string environment) =>
        Extensions.Select(extension => $"**/*.env.{environment}.{extension}");

    /// <summary>
    /// Matches the base files: every project file except environment overlays.
    /// </summary>
    public static Matcher Base()
    {
        var matcher = new Matcher();
        matcher.AddIncludePatterns(AllProjectFiles);
        matcher.AddExcludePatterns(AnyEnvironmentGlobs);
        return matcher;
    }

    /// <summary>
    /// Matches a single environment's overlay files.
    /// </summary>
    public static Matcher Environment(string environment)
    {
        var matcher = new Matcher();
        matcher.AddIncludePatterns(EnvironmentGlobs(environment));
        return matcher;
    }

    /// <summary>
    /// Runs <paramref name="matcher"/> against <paramref name="root"/> and returns the matched files as sorted absolute paths.
    /// </summary>
    public static IReadOnlyList<string> Match(string root, Matcher matcher) => matcher
        .Execute(new DirectoryInfoWrapper(new DirectoryInfo(root)))
        .Files
        .Select(match => Path.GetFullPath(match.Path, root))
        .OrderBy(path => path, StringComparer.Ordinal)
        .ToList();

    /// <summary>
    /// Every project file under <paramref name="directory"/>, recursively and in a stable order, for the callers that
    /// walk the filesystem rather than matching a glob.
    /// </summary>
    public static IReadOnlyList<string> Enumerate(string directory) =>
    [
        .. Extensions
            .SelectMany(extension => Directory.EnumerateFiles(directory, $"*.{extension}", SearchOption.AllDirectories))
            .OrderBy(file => file, StringComparer.Ordinal)
    ];
}
