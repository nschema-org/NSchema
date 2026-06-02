using System.CommandLine;
using Microsoft.Extensions.Configuration;

namespace NSchema.Cli.Configuration;

internal static class NSchemaConfigurationFactory
{
    private const string DefaultConfigurationFile = "nschema.json";

    // Each entry applies one command-line option onto the bound configuration, but only when the option was
    // explicitly specified. Keeping every option here means all command-line handling lives in one place and uses
    // one mechanism, and lists replace rather than merge (which a configuration provider cannot express).
    private static readonly CliOverride[] _overrides =
    [
        CliOverride.For(CliOptions.ConnectionString, (c, v) => c.ConnectionString = v),
        CliOverride.For(CliOptions.Provider, (c, v) => c.Provider = v),
        CliOverride.For(CliOptions.Destructive, (c, v) => c.DestructiveActionPolicy = v),
        CliOverride.For(CliOptions.StateFile, (c, v) => (c.State ??= new StateConfig()).File = v),
        CliOverride.For(CliOptions.AutoApprove, (c, v) => c.AutoApprove = v),
        CliOverride.For(CliOptions.Schema, (c, v) => c.Schemas = [.. v]),
        CliOverride.For(CliOptions.SchemaName, (c, v) => c.SchemaNames = [.. v]),
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
        var path = parseResult.GetValue(CliOptions.Config);

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
