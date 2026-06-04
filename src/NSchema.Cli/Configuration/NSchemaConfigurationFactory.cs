using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;

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

    private static readonly ConfigOverride[] _overrides =
    [
        ConfigOverride.Enum(CliOptions.Database.Provider, EnvironmentVariables.Provider, SelectProvider),
        ConfigOverride.String(CliOptions.Database.ConnectionString, EnvironmentVariables.ConnectionString,
            (c, v) => (c.Provider.Postgres ??= new PostgresProviderConfig()).ConnectionString = v),
        ConfigOverride.String(CliOptions.State.File, EnvironmentVariables.StateFile,
            (c, v) => (c.State.File ??= new FileStateConfig()).Path = v),
        ConfigOverride.String(CliOptions.State.S3Bucket, EnvironmentVariables.StateS3Bucket,
            (c, v) => (c.State.S3 ??= new S3StateConfig()).Bucket = v),
        ConfigOverride.String(CliOptions.State.S3Key, EnvironmentVariables.StateS3Key,
            (c, v) => (c.State.S3 ??= new S3StateConfig()).Key = v),
        ConfigOverride.Enum(CliOptions.Migration.Destructive, EnvironmentVariables.DestructiveActionPolicy,
            (c, v) => c.DestructiveActionPolicy = v),
        ConfigOverride.Cli(CliOptions.Apply.AutoApprove, (c, v) => c.AutoApprove = v),
        ConfigOverride.Cli(CliOptions.Migration.Scope, (c, v) => c.Scope = [.. v]),
        ConfigOverride.Cli(CliOptions.Desired.Format, (c, v) => c.Schema.Format = v),
        ConfigOverride.Cli(CliOptions.Desired.SchemaDir, (c, v) => c.Schema.Directory = v),
        ConfigOverride.Cli(CliOptions.Desired.SchemaGlob, (c, v) => c.Schema.Glob = v),
    ];

    public static NSchemaConfiguration Create(ParseResult parseResult)
    {
        var config = LoadFromFile(parseResult) ?? new NSchemaConfiguration();

        foreach (var @override in _overrides)
        {
            @override.Apply(config, parseResult);
        }

        return config;
    }

    // Ensures the selected provider's section exists so a bare --provider/NSCHEMA_PROVIDER still
    // selects it (its settings then come from the file or the provider-specific overrides).
    private static void SelectProvider(NSchemaConfiguration config, ProviderType? type)
    {
        switch (type)
        {
            case ProviderType.Postgres:
                config.Provider.Postgres ??= new PostgresProviderConfig();
                break;
        }
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

    /// <summary>
    /// A single configuration override: an optional environment variable and a command-line option that
    /// both feed one setting. <see cref="Apply"/> resolves the effective value (CLI over environment over
    /// the loaded file) and writes it onto the configuration.
    /// </summary>
    private abstract class ConfigOverride
    {
        /// <summary>A command-line-only override (no environment variable).</summary>
        public static ConfigOverride Cli<T>(Option<T> option, Action<NSchemaConfiguration, T> apply)
            => new TypedOverride<T>(option, environmentVariable: null, parseEnvironment: null, apply);

        /// <summary>A string-valued override also readable from an environment variable.</summary>
        public static ConfigOverride String(Option<string?> option, string environmentVariable, Action<NSchemaConfiguration, string?> apply)
            => new TypedOverride<string?>(option, environmentVariable, static value => value, apply);

        /// <summary>An enum-valued override also readable (case-insensitively) from an environment variable.</summary>
        public static ConfigOverride Enum<TEnum>(Option<TEnum?> option, string environmentVariable, Action<NSchemaConfiguration, TEnum?> apply)
            where TEnum : struct, Enum
            => new TypedOverride<TEnum?>(option, environmentVariable, static value => System.Enum.Parse<TEnum>(value, ignoreCase: true), apply);

        public abstract void Apply(NSchemaConfiguration config, ParseResult parseResult);

        private sealed class TypedOverride<T>(
            Option<T> option,
            string? environmentVariable,
            Func<string, T>? parseEnvironment,
            Action<NSchemaConfiguration, T> apply) : ConfigOverride
        {
            public override void Apply(NSchemaConfiguration config, ParseResult parseResult)
            {
                if (parseResult.GetResult(option) is { Implicit: false })
                {
                    apply(config, parseResult.GetValue(option)!);
                }
                else if (environmentVariable is not null
                         && Environment.GetEnvironmentVariable(environmentVariable) is { } value)
                {
                    apply(config, parseEnvironment!(value));
                }
            }
        }
    }
}
