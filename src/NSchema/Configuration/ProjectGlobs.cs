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
    /// The base configuration files, loaded for every environment.
    /// </summary>
    public const string BaseConfig = "**/*.env.sql";

    /// <summary>
    /// Any environment-specific configuration file, for any environment.
    /// </summary>
    public const string AnyEnvironmentConfig = "**/*.env.*.sql";

    /// <summary>
    /// The configuration glob for a single environment.
    /// </summary>
    public static string EnvironmentConfig(string environment) => $"**/*.env.{environment}.sql";

    /// <summary>
    /// Matches the schema files: every <c>.sql</c> file except configuration files.
    /// </summary>
    public static Matcher Schema() => new Matcher()
        .AddInclude(AllSql)
        .AddExclude(BaseConfig)
        .AddExclude(AnyEnvironmentConfig);

    /// <summary>
    /// Matches the base configuration files.
    /// </summary>
    public static Matcher BaseConfiguration() => new Matcher()
        .AddInclude(BaseConfig);

    /// <summary>
    /// Matches a single environment's configuration files.
    /// </summary>
    public static Matcher EnvironmentConfiguration(string environment) => new Matcher()
        .AddInclude(EnvironmentConfig(environment));

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
