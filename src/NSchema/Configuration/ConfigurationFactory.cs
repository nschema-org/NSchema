using System.CommandLine;
using NSchema.Configuration.Binding;
using NSchema.Configuration.Ddl;

namespace NSchema.Configuration;

internal static class ConfigurationFactory
{
    /// <summary>
    /// Loads a command's configuration, reading the project's <c>.sql</c> config blocks layered with the given
    /// <paramref name="environment"/>'s overlay (if any), then binding environment variables and CLI options on top.
    /// </summary>
    /// <param name="args">The parsed command line.</param>
    /// <param name="environment">The target environment (resolved by the caller via <see cref="ResolveEnvironment"/>), or <see langword="null"/> for the base config.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    public static async ValueTask<T> Load<T>(ParseResult args, string? environment, CancellationToken cancellationToken = default) where T : class, IBindable, new()
    {
        ApplyWorkingDirectory(args);

        var currentDirectory = Directory.GetCurrentDirectory();
        var projectConfig = await DdlProjectConfigReader.Read(currentDirectory, environment, cancellationToken);
        var config = new T();
        config.Bind(projectConfig, args);
        return config;
    }

    /// <summary>
    /// Resolves the target environment from <c>--environment</c> (or the <c>NSCHEMA_ENVIRONMENT</c> variable), or
    /// <see langword="null"/> when none is selected.
    /// </summary>
    public static string? ResolveEnvironment(ParseResult args) =>
        CommonOptions.Environment.GetValueOrDefault(args, null);

    /// <summary>
    /// Sets the current directory based on <c>--directory</c> before anything is resolved.
    /// </summary>
    private static void ApplyWorkingDirectory(ParseResult args)
    {
        if (!CommonOptions.Directory.TryGetValue(args, out var directory))
        {
            return;
        }

        var currentDirectory = Directory.GetCurrentDirectory();
        var fullPath = Path.Combine(currentDirectory, directory);
        Directory.SetCurrentDirectory(fullPath);
    }
}
