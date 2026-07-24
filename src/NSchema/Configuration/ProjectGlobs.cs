using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;

namespace NSchema.Configuration;

/// <summary>
/// The glob patterns that classify a project's <c>.sql</c> files by role. The <c>.env.</c> marker in a file name
/// makes it configuration: <c>&lt;any&gt;.env.sql</c> loads for every environment, <c>&lt;any&gt;.env.&lt;env&gt;.sql</c>
/// only when that environment is selected. Every other <c>.sql</c> file is schema.
/// </summary>
internal static class ProjectGlobs
{
    /// <summary>
    /// Every SQL file, recursively.
    /// </summary>
    public const string AllSql = "**/*.sql";

    /// <summary>
    /// Any environment-specific configuration file, for any environment.
    /// </summary>
    public const string AnyEnvironmentConfigurationGlob = "**/*.env.*.sql";

    /// <summary>
    /// The configuration glob for a single environment.
    /// </summary>
    public static string EnvironmentConfigurationGlob(string environment) => $"**/*.env.{environment}.sql";

    /// <summary>
    /// Matches the base files: every <c>.sql</c> file except environment files.
    /// </summary>
    public static Matcher Base() => new Matcher()
        .AddInclude(AllSql)
        .AddExclude(AnyEnvironmentConfigurationGlob);

    /// <summary>
    /// Matches a single environment's configuration files.
    /// </summary>
    public static Matcher EnvironmentConfiguration(string environment) => new Matcher()
        .AddInclude(EnvironmentConfigurationGlob(environment));

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
