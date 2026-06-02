using System.CommandLine;
using Microsoft.Extensions.Configuration;

namespace NSchema.Cli.Configuration;

internal static class NSchemaOptionsFactory
{
    private const string DefaultOptionsFile = "nschema.json";

    public static NSchemaOptions Create(ParseResult parseResult)
    {
        var config = LoadConfig(parseResult);
        ApplyCliOverrides(config, parseResult);
        return config;
    }

    private static NSchemaOptions LoadConfig(ParseResult parseResult)
    {
        var builder = new ConfigurationBuilder();

        var path = parseResult.GetValue(CliOptions.Config);
        if (path is null)
        {
            builder.AddJsonFile(DefaultOptionsFile, optional: true);
        }
        else
        {
            builder.AddJsonFile(Path.GetFullPath(path), optional: false);
        }

        builder.AddEnvironmentVariables("NSCHEMA_");

        var config = new NSchemaOptions();
        builder.Build().Bind(config);
        return config;
    }

    // Command-line flags win over anything loaded from config or the environment.
    private static void ApplyCliOverrides(NSchemaOptions config, ParseResult parseResult)
    {
        config.AutoApprove = parseResult.GetValue(CliOptions.AutoApprove);

        if (parseResult.GetValue(CliOptions.ConnectionString) is { } connectionString)
        {
            config.ConnectionString = connectionString;
        }

        if (parseResult.GetValue(CliOptions.Provider) is { } provider)
        {
            config.Provider = provider;
        }

        if (parseResult.GetValue(CliOptions.Schema) is { Length: > 0 } schemas)
        {
            config.Schemas = [.. schemas];
        }

        if (parseResult.GetValue(CliOptions.SchemaName) is { Length: > 0 } schemaNames)
        {
            config.SchemaNames = [.. schemaNames];
        }

        if (parseResult.GetValue(CliOptions.Destructive) is { } destructive)
        {
            config.DestructiveActionPolicy = destructive;
        }

        if (parseResult.GetValue(CliOptions.StateFile) is { } stateFile)
        {
            config.State = new NSchemaOptions.StateConfig { File = stateFile };
        }
    }
}
