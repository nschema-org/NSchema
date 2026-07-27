using System.CommandLine;
using FluentValidation;
using NSchema.Configuration.Binding;

namespace NSchema.Configuration;

internal static class ConfigurationFactory
{
    /// <summary>
    /// Loads a command's configuration, reading the project's configuration files layered with the given
    /// <paramref name="environment"/>'s overlay (if any), then binding environment variables and CLI options on top.
    /// </summary>
    /// <param name="args">The parsed command line.</param>
    /// <param name="environment">The target environment (resolved by the caller via <see cref="ResolveEnvironment"/>), or <see langword="null"/> for the base config.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    public static async ValueTask<Result<T>> Load<T>(ParseResult args, string? environment, CancellationToken cancellationToken = default) where T : class, IBindable, new()
    {
        ApplyWorkingDirectory(args);

        var currentDirectory = Directory.GetCurrentDirectory();
        var projectConfiguration = await ProjectConfigurationReader.Read(currentDirectory, environment, cancellationToken);
        if (projectConfiguration.IsFailure)
        {
            return Result.Failure<T>(projectConfiguration.Diagnostics);
        }

        var config = new T();
        config.Bind(projectConfiguration.Require(), args);

        // Advisory findings from reading the project ride along with the bound config, so a warning is not lost.
        return Result.From(config, projectConfiguration.Diagnostics);
    }

    /// <summary>
    /// Loads a command's configuration as <see cref="Load{T}(ParseResult,string?,CancellationToken)"/> does, then runs
    /// <paramref name="validator"/> over it, folding both sets of findings into one result.
    /// </summary>
    /// <param name="args">The parsed command line.</param>
    /// <param name="environment">The target environment, or <see langword="null"/> for the base config.</param>
    /// <param name="validator">The command's configuration validator.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    public static async ValueTask<Result<T>> Load<T>(ParseResult args, string? environment, IValidator<T> validator, CancellationToken cancellationToken = default)
        where T : class, IBindable, new()
    {
        var loaded = await Load<T>(args, environment, cancellationToken);
        if (loaded.IsFailure)
        {
            return loaded;
        }

        var validated = validator.Check(loaded.Require());
        return Result.From(validated.Value, loaded.Diagnostics.Concat(validated.Diagnostics));
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
    internal static void ApplyWorkingDirectory(ParseResult args)
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
