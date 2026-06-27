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
    /// Any environment overlay, for any environment.
    /// </summary>
    public const string AnyEnvironmentOverlay = "**/*.env.*.sql";

    /// <summary>
    /// The overlay glob for a single environment.
    /// </summary>
    public static string EnvironmentOverlay(string environment) => $"**/*.env.{environment}.sql";

    /// <summary>
    /// Matches the base schema/config files: every <c>.sql</c> file except environment overlays.
    /// </summary>
    public static Matcher BaseSchema() => new Matcher()
        .AddInclude(AllSql)
        .AddExclude(AnyEnvironmentOverlay);

    /// <summary>
    /// Matches a single environment's overlay files.
    /// </summary>
    public static Matcher EnvironmentSchema(string environment) => new Matcher()
        .AddInclude(EnvironmentOverlay(environment));

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
