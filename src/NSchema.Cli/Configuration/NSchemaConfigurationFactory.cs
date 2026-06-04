using System.CommandLine;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using NSchema.Cli.Configuration.Provider;
using NSchema.Cli.Configuration.Schema;
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

    private static NSchemaConfiguration? LoadFromFile(ParseResult parseResult)
    {
        // Relative paths must resolve against the working directory the tool was invoked from.
        var cliPath = parseResult.GetValue(CliOptions.Global.Config);
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
        var value = GetOverride(result, EnvironmentVariables.Provider, CliOptions.Database.Provider);
        if (value == null)
        {
            return;
        }

        var provider = Enum.Parse<ProviderType>(value, ignoreCase: true);
        switch (provider)
        {
            case ProviderType.Postgres:
                config.Provider.Postgres ??= new PostgresProviderConfig();
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private static void ConfigureConnectionString(NSchemaConfiguration config, ParseResult result)
    {
        var value = GetOverride(result, EnvironmentVariables.ConnectionString, CliOptions.Database.ConnectionString);
        if (value == null)
        {
            return;
        }

        config.Provider.Postgres ??= new PostgresProviderConfig();
    }

    private static void ConfigureFileState(NSchemaConfiguration config, ParseResult result)
    {
        var value = GetOverride(result, EnvironmentVariables.StateFile, CliOptions.State.File);
        if (value == null)
        {
            return;
        }
        config.State.File ??= new FileStateConfig();
        config.State.File.Path = value;
    }

    private static void ConfigureS3State(NSchemaConfiguration config, ParseResult result)
    {
        if (TryGetOverride(result, EnvironmentVariables.StateS3Bucket, CliOptions.State.S3Bucket, out var bucket))
        {
            config.State.S3 ??= new S3StateConfig();
            config.State.S3.Bucket = bucket;
        }

        if (TryGetOverride(result, EnvironmentVariables.StateS3Key, CliOptions.State.S3Key, out var key))
        {
            config.State.S3 ??= new S3StateConfig();
            config.State.S3.Key = key;
        }
    }

    private static void ConfigureDestructiveActionPolicy(NSchemaConfiguration config, ParseResult result)
    {
        if (TryGetOverride(result, EnvironmentVariables.DestructiveActionPolicy, CliOptions.Migration.Destructive, Enum.Parse<DestructiveActionPolicy>, out var value))
        {
            config.DestructiveActionPolicy = value;
        }
    }

    private static void ConfigureAutoApprove(NSchemaConfiguration config, ParseResult result)
    {
        if (TryGetOverride(result, null, CliOptions.Apply.AutoApprove, bool.Parse, out var autoApprove))
        {
            config.AutoApprove = autoApprove;
        }
    }

    private static void ConfigureMigrationScope(NSchemaConfiguration config, ParseResult result)
    {
        if (TryGetOverride(result, null, CliOptions.Migration.Scope, out var scope))
        {
            config.Scope = scope;
        }
    }

    private static void ConfigureSchema(NSchemaConfiguration config, ParseResult result)
    {
        if (TryGetOverride(result, null, CliOptions.Schema.Format, Enum.Parse<SchemaFormat>, out var format))
        {
            config.Schema.Format = format;
        }

        if (TryGetOverride(result, null, CliOptions.Schema.Directory, out var directory))
        {
            config.Schema.Directory = directory;
        }

        if (TryGetOverride(result, null, CliOptions.Schema.Pattern, out var pattern))
        {
            config.Schema.Pattern = pattern;
        }
    }

    private static bool TryGetOverride(ParseResult parseResult, string? envVar, Option<string> option, [NotNullWhen(true)]out string? value)
    {
        return TryGetOverride<string?>(parseResult, envVar, option, s => s, out value);
    }

    private static bool TryGetOverride<T>(ParseResult result, string? envVar, Option<T> option, Func<string, T> parse, out T? value)
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
                value = parse(envValue);
                return true;
            }
        }

        value = default;
        return false;
    }
}
