using Microsoft.Extensions.DependencyInjection;
using NSchema.Cli.Configuration;
using NSchema.Cli.Services;
using NSchema.Hosting;
using NSchema.Yaml;

namespace NSchema.Cli;

/// <summary>
/// Translates resolved <see cref="NSchemaConfiguration"/> into a configured <see cref="NSchemaApplication"/>.
/// </summary>
internal static class ApplicationFactory
{
    public static NSchemaApplication Create(NSchemaConfiguration configuration, bool requireDesiredSchema = true)
    {
        var builder = NSchemaApplication.CreateBuilder()
            .WithExceptionBehavior(ExceptionBehavior.Throw);

        builder.Services.AddSingleton(configuration);
        builder.Services.AddSingleton<IMigrationConfirmation, ConsoleMigrationConfirmation>();

        ConfigurePolicies(builder, configuration);
        if (requireDesiredSchema)
        {
            ConfigureDesiredSchema(builder, configuration);
        }

        ConfigureScope(builder, configuration);
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
        var schema = configuration.Schema;
        if (string.IsNullOrWhiteSpace(schema?.Directory))
        {
            throw new InvalidOperationException("No schema directory configured. Set \"schema.dir\" in nschema.json or pass --schema-dir.");
        }

        // Resolve the directory to an absolute root.
        var root = Path.GetFullPath(schema.Directory, Directory.GetCurrentDirectory());
        var pattern = string.IsNullOrWhiteSpace(schema.Glob) ? schema.Format.DefaultGlob() : schema.Glob;
        var glob = $"{root}/{pattern}";

        switch (schema.Format)
        {
            case SchemaFormat.Yaml: builder.AddYamlSchemasFromGlob(glob); break;
            case SchemaFormat.Json: builder.AddJsonSchemasFromGlob(glob); break;
            default: throw new ArgumentOutOfRangeException(nameof(configuration), schema.Format, "Unknown schema format.");
        }
    }

    private static void ConfigureScope(NSchemaApplicationBuilder builder, NSchemaConfiguration configuration)
    {
        if (configuration.Scope.Count > 0)
        {
            builder.ForSchemas([.. configuration.Scope]);
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
