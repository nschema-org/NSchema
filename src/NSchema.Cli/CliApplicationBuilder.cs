using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using NSchema.Aws;
using NSchema.Cli.Configuration;
using NSchema.Cli.Configuration.Provider;
using NSchema.Cli.Configuration.Schema;
using NSchema.Cli.Configuration.State;
using NSchema.Cli.Extensions;
using NSchema.Cli.Services;
using NSchema.Hosting;
using NSchema.Postgres;
using NSchema.Yaml;

namespace NSchema.Cli;

/// <summary>
/// Translates resolved <see cref="NSchemaConfiguration"/> into a configured <see cref="NSchemaApplication"/>.
/// </summary>
internal sealed class CliApplicationBuilder
{
    private static readonly SchemaConfigValidator _schemaValidator = new();
    private static readonly ProviderConfigValidator _providerValidator = new();
    private static readonly StateConfigValidator _stateValidator = new();

    private readonly NSchemaConfiguration _configuration;

    private readonly NSchemaApplicationBuilder _builder = NSchemaApplication.CreateBuilder();

    private CliApplicationBuilder(NSchemaConfiguration configuration)
    {
        _configuration = configuration;
        _builder.WithExceptionBehavior(ExceptionBehavior.Throw);
        _builder.Services.AddSingleton(configuration);
    }

    public CliApplicationBuilder ConfigurePolicies()
    {
        if (_configuration.DestructiveActionPolicy is { } policy)
        {
            _builder.WithDestructiveActionPolicy(policy);
        }

        return this;
    }

    public CliApplicationBuilder ConfigureDesiredSchema()
    {
        _schemaValidator.ValidateOrThrow(_configuration.Schema);

        // The property pattern binds the directory non-null; validation above guarantees it is present.
        if (_configuration.Schema is { Directory: { } directory } schema)
        {
            var root = Path.GetFullPath(directory, Directory.GetCurrentDirectory());
            var pattern = string.IsNullOrWhiteSpace(schema.Pattern) ? schema.Format.DefaultPattern() : schema.Pattern;
            var glob = $"{root}/{pattern}";

            switch (schema.Format)
            {
                case SchemaFormat.Yaml: _builder.AddYamlSchemasFromGlob(glob); break;
                case SchemaFormat.Json: _builder.AddJsonSchemasFromGlob(glob); break;
                default: throw new UnreachableException();
            }
        }

        return this;
    }

    public CliApplicationBuilder ConfigureScope()
    {
        if (_configuration.Scope is { Length: > 0 } scope)
        {
            _builder.ForSchemas(scope);
        }
        return this;
    }

    public CliApplicationBuilder ConfigureBackendState()
    {
        _stateValidator.ValidateOrThrow(_configuration.State);

        // The property patterns bind non-null locals; validation above guarantees a case matches.
        switch (_configuration.State)
        {
            case { File: { } file }:
                _builder.UseFileStateStore(file.Path);
                break;
            case { S3: { } s3 }:
                _builder.UseStateStoreS3(s3.Bucket, s3.Key);
                break;
        }

        return this;
    }

    public CliApplicationBuilder ConfigureDatabaseProvider()
    {
        _providerValidator.ValidateOrThrow(_configuration.Provider);

        // The property pattern binds non-null locals; validation above guarantees it matches.
        if (_configuration.Provider is { Postgres: { } postgres })
        {
            _builder.UseCurrentSchemaPostgres(dataSource =>
            {
                dataSource.ConnectionStringBuilder.ConnectionString = postgres.ConnectionString;
                if (postgres.CommandTimeout is { } commandTimeout)
                {
                    dataSource.ConnectionStringBuilder.CommandTimeout = commandTimeout;
                }
            });
        }

        return this;
    }

    public CliApplicationBuilder ConfigureConfirmation()
    {
        _builder.Services.AddSingleton<IMigrationConfirmation, ConsoleMigrationConfirmation>();
        return this;
    }

    public NSchemaApplication Build() => _builder.Build();

    public static CliApplicationBuilder Create(NSchemaConfiguration configuration) => new(configuration);
}
