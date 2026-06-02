using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;
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

    // Apply an explicitly-specified command-line option over the loaded configuration.
    private static readonly CliOverride[] _cliOverrides =
    [
        CliOverride.For(CliOptions.Database.Provider, (c, v) => c.Provider.Type = v),
        CliOverride.For(CliOptions.Database.ConnectionString, (c, v) => c.Provider.ConnectionString = v),
        CliOverride.For(CliOptions.State.Type, (c, v) => c.State.Type = v),
        CliOverride.For(CliOptions.State.ConnectionString, (c, v) => c.State.ConnectionString = v),
        CliOverride.For(CliOptions.State.File, (c, v) => { c.State.Type = StateType.File; c.State.ConnectionString = v; }),
        CliOverride.For(CliOptions.Migration.Destructive, (c, v) => c.DestructiveActionPolicy = v),
        CliOverride.For(CliOptions.Apply.AutoApprove, (c, v) => c.AutoApprove = v),
        CliOverride.For(CliOptions.Migration.Scope, (c, v) => c.Scope = [.. v]),
        CliOverride.For(CliOptions.Desired.Format, (c, v) => c.Schema.Format = v),
        CliOverride.For(CliOptions.Desired.SchemaDir, (c, v) => c.Schema.Directory = v),
        CliOverride.For(CliOptions.Desired.SchemaGlob, (c, v) => c.Schema.Glob = v),
    ];

    // Apply a recognized environment variable over the loaded configuration.
    private static readonly (string Variable, Action<NSchemaConfiguration, string> Apply)[] _environmentOverrides =
    [
        ("NSCHEMA_PROVIDER", (c, v) => c.Provider.Type = Enum.Parse<ProviderType>(v, ignoreCase: true)),
        ("NSCHEMA_CONNECTION_STRING", (c, v) => c.Provider.ConnectionString = v),
        ("NSCHEMA_STATE_TYPE", (c, v) => c.State.Type = Enum.Parse<StateType>(v, ignoreCase: true)),
        ("NSCHEMA_STATE_CONNECTION_STRING", (c, v) => c.State.ConnectionString = v),
        ("NSCHEMA_DESTRUCTIVE_ACTION_POLICY", (c, v) => c.DestructiveActionPolicy = Enum.Parse<DestructiveActionPolicy>(v, ignoreCase: true)),
    ];

    public static NSchemaConfiguration Create(ParseResult parseResult)
    {
        // Load base file.
        var config = LoadFromFile(parseResult) ?? new NSchemaConfiguration();

        // Apply environment variable overrides
        foreach (var (variable, apply) in _environmentOverrides)
        {
            if (Environment.GetEnvironmentVariable(variable) is { } value)
            {
                apply(config, value);
            }
        }

        // Apply CLI argument overrides
        foreach (var cliOverride in _cliOverrides)
        {
            cliOverride.Apply(config, parseResult);
        }

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

    /// <summary>
    /// Applies a single command-line option onto the configuration, but only when it was explicitly specified.
    /// </summary>
    private sealed class CliOverride
    {
        private readonly Func<ParseResult, bool> _shouldApply;
        private readonly Action<NSchemaConfiguration, ParseResult> _apply;

        private CliOverride(Func<ParseResult, bool> shouldApply, Action<NSchemaConfiguration, ParseResult> apply)
        {
            _shouldApply = shouldApply;
            _apply = apply;
        }

        public static CliOverride For<T>(Option<T> option, Action<NSchemaConfiguration, T> apply) => new(
            pr => pr.GetResult(option) is { Implicit: false },
            (config, result) => apply(config, result.GetValue(option)!)
        );

        public void Apply(NSchemaConfiguration config, ParseResult parseResult)
        {
            if (_shouldApply(parseResult))
            {
                _apply(config, parseResult);
            }
        }
    }
}
