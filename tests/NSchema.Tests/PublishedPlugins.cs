using NSchema.Configuration.Domain;
using NSchema.Configuration.Plugins;

namespace NSchema.Tests;

/// <summary>
/// The newest published version of a plugin package this host can load, for the tests that restore real plugins.
/// </summary>
internal static class PublishedPlugins
{
    // Resolved once per run: the lookup shells out to `dotnet package search`, far too slow to repeat per test.
    private static readonly Lazy<SemanticVersion> _postgres = new(() => Latest("NSchema.Postgres"));
    private static readonly Lazy<SemanticVersion> _sqlServer = new(() => Latest("NSchema.SqlServer"));
    private static readonly Lazy<SemanticVersion> _sqlite = new(() => Latest("NSchema.Sqlite"));

    public static SemanticVersion Postgres => _postgres.Value;

    public static SemanticVersion SqlServer => _sqlServer.Value;

    public static SemanticVersion Sqlite => _sqlite.Value;

    /// <summary>
    /// The published version of a package by name, resolved once however many tests ask for it.
    /// </summary>
    public static SemanticVersion Of(string package) => package switch
    {
        "NSchema.Postgres" => Postgres,
        "NSchema.SqlServer" => SqlServer,
        "NSchema.Sqlite" => Sqlite,
        _ => throw new InvalidOperationException($"No published version is tracked for '{package}'."),
    };

    private static SemanticVersion Latest(string package)
    {
        var resolved = new PluginLoader().ResolveLatestVersion(new PackageId(package));
        if (resolved.IsSuccess)
        {
            return resolved.Require();
        }

        // A test helper that cannot resolve is an environment problem, not a failed assertion — say which.
        throw new InvalidOperationException(
            $"Could not resolve a published version of '{package}'. The tests that restore real plugins need the "
            + $".NET SDK and network access. {string.Join("; ", resolved.Errors.Select(error => error.Message))}");
    }
}
