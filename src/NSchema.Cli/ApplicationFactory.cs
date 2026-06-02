using Microsoft.Extensions.DependencyInjection;
using NSchema.Cli.Configuration;
using NSchema.Cli.Services;
using NSchema.Hosting;

namespace NSchema.Cli;

/// <summary>
/// Translates resolved <see cref="NSchemaOptions"/> into a configured <see cref="NSchemaApplication"/>.
/// </summary>
internal static class ApplicationFactory
{
    public static NSchemaApplication Create(NSchemaOptions options)
    {
        var builder = NSchemaApplication.CreateBuilder();
        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton<IMigrationConfirmation, ConsoleMigrationConfirmation>();

        ConfigurePolicies(builder, options);
        ConfigureDesiredSchema(builder, options);
        ConfigureBackendState(builder, options);
        ConfigureDatabaseProvider(builder, options);

        return builder.Build();
    }

    private static void ConfigurePolicies(NSchemaApplicationBuilder builder, NSchemaOptions options)
    {
        if (options.DestructiveActionPolicy is { } policy)
        {
            builder.WithDestructiveActionPolicy(policy);
        }
    }

    private static void ConfigureDesiredSchema(NSchemaApplicationBuilder builder, NSchemaOptions options)
    {
        // TODO: Add YAML support.
        foreach (var schema in options.Schemas)
        {
            builder.AddJsonSchemasFromGlob(schema);
        }

        if (options.SchemaNames.Count > 0)
        {
            builder.ForSchemas([.. options.SchemaNames]);
        }
    }

    private static void ConfigureBackendState(NSchemaApplicationBuilder builder, NSchemaOptions options)
    {
        // TODO: Add AWS S3 support.
        if (options.State?.File is { } stateFile)
        {
            builder.UseFileStateStore(stateFile);
        }
    }

    private static void ConfigureDatabaseProvider(NSchemaApplicationBuilder builder, NSchemaOptions options)
    {
        // TODO: Add postgres support.
        if (options.Provider is { Length: > 0 } provider)
        {
            throw new NotSupportedException(
                $"Database provider '{provider}' is not available in this build. No database providers are bundled yet, " +
                "so only offline operations (plan/refresh against a state file) are supported.");
        }
    }
}
