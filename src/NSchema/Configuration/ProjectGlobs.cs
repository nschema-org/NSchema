using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;

namespace NSchema.Configuration;

/// <summary>
/// The glob patterns that classify a project's <c>.sql</c> files by role.
/// </summary>
internal static class ProjectGlobs
{
    /// <summary>
    /// Every SQL file, recursively.
    /// </summary>
    public const string AllSql = "**/*.sql";

    /// <summary>
    /// Any environment overlay file, for any environment.
    /// </summary>
    public const string AnyEnvironmentGlob = "**/*.env.*.sql";

    /// <summary>
    /// The overlay glob for a single environment.
    /// </summary>
    public static string EnvironmentGlob(string environment) => $"**/*.env.{environment}.sql";

    /// <summary>
    /// Matches the base files: every <c>.sql</c> file except environment overlays.
    /// </summary>
    public static Matcher Base() => new Matcher()
        .AddInclude(AllSql)
        .AddExclude(AnyEnvironmentGlob);

    /// <summary>
    /// Matches a single environment's overlay files.
    /// </summary>
    public static Matcher Environment(string environment) => new Matcher()
        .AddInclude(EnvironmentGlob(environment));

    /// <summary>
    /// Runs <paramref name="matcher"/> against <paramref name="root"/> and returns the matched files as sorted absolute paths.
    /// </summary>
    public static IReadOnlyList<string> Match(string root, Matcher matcher) => matcher
        .Execute(new DirectoryInfoWrapper(new DirectoryInfo(root)))
        .Files
        .Select(match => Path.GetFullPath(match.Path, root))
        .OrderBy(path => path, StringComparer.Ordinal)
        .ToList();
}
