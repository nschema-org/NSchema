using System.CommandLine;
using Microsoft.Extensions.Configuration;

namespace NSchema.Cli.Configuration;

internal static class NSchemaConfigurationFactory
{
    private const string DefaultConfigurationFile = "nschema.json";

    public static NSchemaConfiguration Create(ParseResult parseResult)
    {
        var builder = new ConfigurationBuilder();
        AddConfigurationFile(builder, parseResult);
        builder.AddEnvironmentVariables("NSCHEMA_");
        builder.Add(new ParseResultConfigurationSource(parseResult));

        var config = new NSchemaConfiguration();
        builder.Build().Bind(config);

        ApplyListOverrides(config, parseResult);
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

    // ConfigurationBinder merges list elements by index across providers, so a CLI-supplied list would overlay
    // rather than replace the config file's. Apply list options explicitly to preserve "command-line wins" semantics.
    private static void ApplyListOverrides(NSchemaConfiguration config, ParseResult parseResult)
    {
        if (parseResult.GetResult(CliOptions.Schema) is { Implicit: false })
        {
            config.Schemas = [.. parseResult.GetValue(CliOptions.Schema)!];
        }

        if (parseResult.GetResult(CliOptions.SchemaName) is { Implicit: false })
        {
            config.SchemaNames = [.. parseResult.GetValue(CliOptions.SchemaName)!];
        }
    }
}
