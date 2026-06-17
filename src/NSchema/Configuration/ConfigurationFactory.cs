using System.CommandLine;
using NSchema.Configuration.Binding;
using NSchema.Configuration.Ddl;

namespace NSchema.Configuration;

internal static class ConfigurationFactory
{
    public static async ValueTask<T> Load<T>(ParseResult args, CancellationToken cancellationToken = default) where T : class, IBindable, new()
    {
        ApplyWorkingDirectory(args);

        var currentDirectory = Directory.GetCurrentDirectory();
        var environment = CommonOptions.Environment.GetValueOrDefault(null, args, null);
        var projectConfig = await DdlProjectConfigReader.Read(currentDirectory, environment, cancellationToken);
        var config = new T();
        config.Bind(projectConfig, args);
        return config;
    }

    /// <summary>
    /// Sets the current directory based on <c>--directory</c> before anything is resolved.
    /// </summary>
    private static void ApplyWorkingDirectory(ParseResult args)
    {
        if (!CommonOptions.Directory.TryGetValue(null, args, out var directory))
        {
            return;
        }

        var currentDirectory = Directory.GetCurrentDirectory();
        var fullPath = Path.Combine(currentDirectory, directory);
        Directory.SetCurrentDirectory(fullPath);
    }
}
