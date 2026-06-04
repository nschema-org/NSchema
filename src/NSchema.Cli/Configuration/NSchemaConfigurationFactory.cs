using System.CommandLine;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using NSchema.Cli.Configuration.Provider;
using NSchema.Cli.Configuration.State;
using NSchema.Migration;

namespace NSchema.Cli.Configuration;

internal static class NSchemaConfigurationFactory
{
    private const string DefaultConfigurationFile = "nschema.json";

    /// <summary>
    /// The serializer options used to read and write <c>nschema.json</c>.
    /// </summary>
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static NSchemaConfiguration Create(ParseResult result)
    {
        var config = LoadFromFile(result) ?? new NSchemaConfiguration();

        ConfigureProvider(config, result);
        ConfigureConnectionString(config, result);
        ConfigureFileState(config, result);
        ConfigureS3State(config, result);
        ConfigureDestructiveActionPolicy(config, result);
        ConfigureAutoApprove(config, result);
        ConfigureMigrationScope(config, result);
        ConfigureSchema(config, result);

        return config;
    }

    private static NSchemaConfiguration? LoadFromFile(ParseResult result)
    {
        // Relative paths must resolve against the working directory the tool was invoked from.
        var cliPath = result.GetValue(CliOptions.Global.Config);
        var configFile = Path.GetFullPath(cliPath ?? DefaultConfigurationFile, Directory.GetCurrentDirectory());

        if (!File.Exists(configFile))
        {
            // An explicit --config must exist; the default file is optional.
            return cliPath is null ? null : throw new FileNotFoundException($"Config file not found: \"{configFile}\".", configFile);
        }

        using var stream = File.OpenRead(configFile);
        return JsonSerializer.Deserialize<NSchemaConfiguration>(stream, JsonOptions)
               ?? throw new InvalidOperationException($"Failed to parse config file \"{configFile}\".");
    }

    private static void ConfigureProvider(NSchemaConfiguration config, ParseResult result)
    {
        if (TryGetOverride(result, CliOptions.Provider.Type, EnvironmentVariables.Provider, Enum.Parse<ProviderType>, out var provider))
        {
            switch (provider)
            {
                case ProviderType.Postgres:
                    config.Provider.Postgres ??= new PostgresProviderConfig();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(provider), $"Unsupported provider type: {provider}");
            }
        }
    }

    private static void ConfigureConnectionString(NSchemaConfiguration config, ParseResult result)
    {
        if (TryGetOverride(result, CliOptions.Provider.ConnectionString, EnvironmentVariables.ConnectionString, out var connectionString))
        {
            config.Provider.Postgres ??= new PostgresProviderConfig();
            config.Provider.Postgres.ConnectionString = connectionString;
        }
    }

    private static void ConfigureFileState(NSchemaConfiguration config, ParseResult result)
    {
        if (TryGetOverride(result, CliOptions.State.File, EnvironmentVariables.StateFile, out var path))
        {
            config.State.File ??= new FileStateConfig();
            config.State.File.Path = path;
        }
    }

    private static void ConfigureS3State(NSchemaConfiguration config, ParseResult result)
    {
        if (TryGetOverride(result, CliOptions.State.S3Bucket, EnvironmentVariables.StateS3Bucket, out var bucket))
        {
            config.State.S3 ??= new S3StateConfig();
            config.State.S3.Bucket = bucket;
        }

        if (TryGetOverride(result, CliOptions.State.S3Key, EnvironmentVariables.StateS3Key, out var key))
        {
            config.State.S3 ??= new S3StateConfig();
            config.State.S3.Key = key;
        }
    }

    private static void ConfigureDestructiveActionPolicy(NSchemaConfiguration config, ParseResult result)
    {
        if (TryGetOverride(result, CliOptions.Migration.Destructive, EnvironmentVariables.DestructiveActionPolicy, Enum.Parse<DestructiveActionPolicy>, out var value))
        {
            config.DestructiveActionPolicy = value;
        }
    }

    private static void ConfigureAutoApprove(NSchemaConfiguration config, ParseResult result)
    {
        if (TryGetOverride(result, CliOptions.Apply.AutoApprove, out var autoApprove))
        {
            config.AutoApprove = autoApprove;
        }
    }

    private static void ConfigureMigrationScope(NSchemaConfiguration config, ParseResult result)
    {
        if (TryGetOverride(result, CliOptions.Migration.Scope, out var scope))
        {
            config.Scope = scope;
        }
    }

    private static void ConfigureSchema(NSchemaConfiguration config, ParseResult result)
    {
        if (TryGetOverride(result, CliOptions.Schema.Format, out var format))
        {
            config.Schema.Format = format;
        }

        if (TryGetOverride(result, CliOptions.Schema.Directory, out var directory))
        {
            config.Schema.Directory = directory;
        }

        if (TryGetOverride(result, CliOptions.Schema.Pattern, out var pattern))
        {
            config.Schema.Pattern = pattern;
        }
    }

    /// <summary>
    /// Tries to get an override value from the given CLI option.
    /// </summary>
    private static bool TryGetOverride<T>(ParseResult result, Option<T> option, out T? value)
    {
        return TryGetOverride(result, option, null, null, out value);
    }

    /// <summary>
    /// Tries to get a string value from the given CLI option or environment variable.
    /// </summary>
    private static bool TryGetOverride(ParseResult parseResult, Option<string> option, string? envVar, [NotNullWhen(true)] out string? value)
    {
        return TryGetOverride<string>(parseResult, option, envVar, s => s, out value);
    }

    /// <summary>
    /// Tries to get a string value from the given CLI option or environment variable.
    /// </summary>
    /// <param name="result">The parse result containing the command line arguments.</param>
    /// <param name="option">The option to check for an override value.</param>
    /// <param name="envVar">The environment variable to check for an override value.</param>
    /// <param name="envParser">A function to parse the environment variable value, if specified.</param>
    /// <param name="value">The override value, if found.</param>
    /// <typeparam name="T">The type of the option value.</typeparam>
    /// <returns><see langword="true"/> if an override value was found; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown if an environment variable override is specified without a value parser.</exception>
    private static bool TryGetOverride<T>(ParseResult result, Option<T> option, string? envVar, Func<string, T>? envParser, out T? value)
    {
        if (result.GetResult(option) is { Implicit: false } argument)
        {
            value = argument.GetRequiredValue(option);
            return true;
        }

        if (envVar != null)
        {
            if (Environment.GetEnvironmentVariable(envVar) is { } envValue)
            {
                if (envParser == null)
                {
                    throw new InvalidOperationException("Environment variable override specified without value parser.");
                }
                value = envParser(envValue);
                return true;
            }
        }

        value = default;
        return false;
    }
}
