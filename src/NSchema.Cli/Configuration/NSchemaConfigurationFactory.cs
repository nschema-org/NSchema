using System.CommandLine;
using Microsoft.Extensions.Configuration;

namespace NSchema.Cli.Configuration;

internal static class NSchemaConfigurationFactory
{
    private const string DefaultConfigurationFile = "nschema.json";

    // Allow CLI args to override default settings.
    private static readonly CliOverride[] _overrides =
    [
        CliOverride.For(CliOptions.Global.ConnectionString, (c, v) => c.ConnectionString = v),
        CliOverride.For(CliOptions.Global.Provider, (c, v) => c.Provider = v),
        CliOverride.For(CliOptions.Global.StateFile, (c, v) => c.State.File = v),
        CliOverride.For(CliOptions.Apply.Destructive, (c, v) => c.DestructiveActionPolicy = v),
        CliOverride.For(CliOptions.Apply.AutoApprove, (c, v) => c.AutoApprove = v),
        CliOverride.For(CliOptions.Desired.Scope, (c, v) => c.Scope = [.. v]),
        CliOverride.For(CliOptions.Desired.Format, (c, v) => c.Schema.Format = v),
        CliOverride.For(CliOptions.Desired.SchemaDir, (c, v) => c.Schema.Directory = v),
        CliOverride.For(CliOptions.Desired.SchemaGlob, (c, v) => c.Schema.Glob = v),
    ];

    public static NSchemaConfiguration Create(ParseResult parseResult)
    {
        // Precedence, lowest to highest: config file, then NSCHEMA_ environment variables, then command-line
        // flags applied over the top of the bound result.
        var builder = new ConfigurationBuilder();
        AddConfigurationFile(builder, parseResult);
        builder.AddEnvironmentVariables("NSCHEMA_");

        var config = new NSchemaConfiguration();
        builder.Build().Bind(config);

        foreach (var cliOverride in _overrides)
        {
            cliOverride.Apply(config, parseResult);
        }

        return config;
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
