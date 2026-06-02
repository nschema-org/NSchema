using System.CommandLine;
using Microsoft.Extensions.Configuration;

namespace NSchema.Cli.Configuration;

internal static class NSchemaConfigurationFactory
{
    private const string DefaultConfigurationFile = "nschema.json";

    // Allow CLI args to override default settings.
    private static readonly CliOverride[] _overrides =
    [
        CliOverride.For(CliOptions.Database.Provider, (c, v) => c.Provider.Type = v),
        CliOverride.For(CliOptions.Database.ConnectionString, (c, v) => c.Provider.ConnectionString = v),
        CliOverride.For(CliOptions.State.Type, (c, v) => c.State.Type = v),
        CliOverride.For(CliOptions.State.ConnectionString, (c, v) => c.State.ConnectionString = v),
        CliOverride.For(CliOptions.State.File, (c, v) =>
        {
            c.State.Type = StateType.File;
            c.State.ConnectionString = v;
        }),
        CliOverride.For(CliOptions.Migration.Destructive, (c, v) => c.DestructiveActionPolicy = v),
        CliOverride.For(CliOptions.Apply.AutoApprove, (c, v) => c.AutoApprove = v),
        CliOverride.For(CliOptions.Migration.Scope, (c, v) => c.Scope = [.. v]),
        CliOverride.For(CliOptions.Desired.Format, (c, v) => c.Schema.Format = v),
        CliOverride.For(CliOptions.Desired.SchemaDir, (c, v) => c.Schema.Directory = v),
        CliOverride.For(CliOptions.Desired.SchemaGlob, (c, v) => c.Schema.Glob = v),
    ];

    // The environment variables recognized by the CLI, mapped to configuration keys.
    private static readonly (string Variable, string Key)[] _environmentVariables =
    [
        ("NSCHEMA_PROVIDER", $"{nameof(NSchemaConfiguration.Provider)}:{nameof(ProviderConfig.Type)}"),
        ("NSCHEMA_CONNECTION_STRING", $"{nameof(NSchemaConfiguration.Provider)}:{nameof(ProviderConfig.ConnectionString)}"),
        ("NSCHEMA_STATE_TYPE", $"{nameof(NSchemaConfiguration.State)}:{nameof(StateConfig.Type)}"),
        ("NSCHEMA_STATE_CONNECTION_STRING", $"{nameof(NSchemaConfiguration.State)}:{nameof(StateConfig.ConnectionString)}"),
        ("NSCHEMA_DESTRUCTIVE_ACTION_POLICY", nameof(NSchemaConfiguration.DestructiveActionPolicy)),
    ];

    public static NSchemaConfiguration Create(ParseResult parseResult)
    {
        // Precedence, lowest to highest: config file, then environment variables, then command-line flags
        // applied over the top of the bound result.
        var builder = new ConfigurationBuilder();
        AddConfigurationFile(builder, parseResult);
        AddEnvironmentVariables(builder);

        var config = new NSchemaConfiguration();
        builder.Build().Bind(config);

        foreach (var cliOverride in _overrides)
        {
            cliOverride.Apply(config, parseResult);
        }

        return config;
    }

    private static void AddEnvironmentVariables(IConfigurationBuilder builder)
    {
        var values = new Dictionary<string, string?>();
        foreach (var (variable, key) in _environmentVariables)
        {
            if (Environment.GetEnvironmentVariable(variable) is { } value)
            {
                values[key] = value;
            }
        }

        if (values.Count > 0)
        {
            builder.AddInMemoryCollection(values);
        }
    }

    private static void AddConfigurationFile(IConfigurationBuilder builder, ParseResult parseResult)
    {
        var path = parseResult.GetValue(CliOptions.Global.Config);

        // Relative paths must resolve against the working directory the tool was invoked from (the user's
        // project), not AppContext.BaseDirectory, which for a packaged dotnet tool is its install location.
        builder.AddJsonFile(
            Path.GetFullPath(path ?? DefaultConfigurationFile, Directory.GetCurrentDirectory()),
            optional: path is null
        );
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
