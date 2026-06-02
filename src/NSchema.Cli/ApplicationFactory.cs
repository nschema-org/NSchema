using Microsoft.Extensions.DependencyInjection;
using NSchema.Cli.Configuration;
using NSchema.Cli.Services;
using NSchema.Hosting;

namespace NSchema.Cli;

/// <summary>
/// Translates resolved <see cref="NSchemaConfiguration"/> into a configured <see cref="NSchemaApplication"/>.
/// </summary>
internal static class ApplicationFactory
{
    public static NSchemaApplication Create(NSchemaConfiguration configuration)
    {
        var builder = NSchemaApplication.CreateBuilder();
        builder.Services.AddSingleton(configuration);
        builder.Services.AddSingleton<IMigrationConfirmation, ConsoleMigrationConfirmation>();

        ConfigurePolicies(builder, configuration);
        ConfigureDesiredSchema(builder, configuration);
        ConfigureBackendState(builder, configuration);
        ConfigureDatabaseProvider(builder, configuration);

        return builder.Build();
    }

    private static void ConfigurePolicies(NSchemaApplicationBuilder builder, NSchemaConfiguration configuration)
    {
        if (configuration.DestructiveActionPolicy is { } policy)
        {
            builder.WithDestructiveActionPolicy(policy);
        }
    }

    private static void ConfigureDesiredSchema(NSchemaApplicationBuilder builder, NSchemaConfiguration configuration)
    {
        // TODO: Add YAML support.
        foreach (var schema in configuration.Schemas)
        {
            builder.AddJsonSchemasFromGlob(schema);
        }

        if (configuration.SchemaNames.Count > 0)
        {
            builder.ForSchemas([.. configuration.SchemaNames]);
        }
    }

    private static void ConfigureBackendState(NSchemaApplicationBuilder builder, NSchemaConfiguration configuration)
    {
        // TODO: Add AWS S3 support.
        if (configuration.State?.File is { } stateFile)
        {
            builder.UseFileStateStore(stateFile);
        }
    }

    private static void ConfigureDatabaseProvider(NSchemaApplicationBuilder builder, NSchemaConfiguration configuration)
    {
        // TODO: Add postgres support.
        if (configuration.Provider is { Length: > 0 } provider)
        {
            throw new NotSupportedException(
                $"Database provider '{provider}' is not available in this build. No database providers are bundled yet, " +
                "so only offline operations (plan/refresh against a state file) are supported.");
        }
    }
}
