using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using NSchema.Aws;
using NSchema.Cli.Configuration;
using NSchema.Cli.Configuration.Provider;
using NSchema.Cli.Configuration.Schema;
using NSchema.Cli.Configuration.State;
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
        var schema = _configuration.Schema;
        if (string.IsNullOrWhiteSpace(schema.Directory))
        {
            throw new InvalidOperationException("No schema directory configured. Set \"schema.dir\" in nschema.json or pass --schema-dir.");
        }

        // Resolve the directory to an absolute root.
        var root = Path.GetFullPath(schema.Directory, Directory.GetCurrentDirectory());
        var pattern = string.IsNullOrWhiteSpace(schema.Pattern) ? schema.Format.DefaultPattern() : schema.Pattern;
        var glob = $"{root}/{pattern}";

        switch (schema.Format)
        {
            case SchemaFormat.Yaml: _builder.AddYamlSchemasFromGlob(glob); break;
            case SchemaFormat.Json: _builder.AddJsonSchemasFromGlob(glob); break;
            default: throw new ArgumentOutOfRangeException(nameof(_configuration), schema.Format, "Unknown schema format.");
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
        // No store configured: plan/apply run online-only and refresh has nowhere to write.
        if (_configuration.State.SelectedType is null)
        {
            return this;
        }

        Validate(_stateValidator, _configuration.State);

        // The property patterns bind non-null locals; validation above guarantees a case matches.
        switch (_configuration.State)
        {
            case { File: { Path: { } path } }:
                _builder.UseFileStateStore(path);
                break;
            case { S3: { Bucket: { } bucket, Key: { } key } }:
                _builder.UseStateStoreS3(bucket, key);
                break;
        }

        return this;
    }

    public CliApplicationBuilder ConfigureDatabaseProvider()
    {
        // No provider configured: only offline operations (plan/refresh against the state store) are available.
        if (_configuration.Provider.SelectedType is null)
        {
            return this;
        }

        Validate(_providerValidator, _configuration.Provider);

        // The property pattern binds non-null locals; validation above guarantees it matches.
        if (_configuration.Provider is { Postgres: { ConnectionString: { } connectionString } postgres })
        {
            _builder.UseCurrentSchemaPostgres(dataSource =>
            {
                dataSource.ConnectionStringBuilder.ConnectionString = connectionString;
                if (postgres.CommandTimeout is { } commandTimeout)
                {
                    dataSource.ConnectionStringBuilder.CommandTimeout = commandTimeout;
                }
            });
        }

        return this;
    }

    // Runs a validator and surfaces any failures as a single error before the run begins. The core
    // owns run-time error presentation, so the CLI fails fast here with the aggregated messages.
    private static void Validate<T>(IValidator<T> validator, T instance)
    {
        var result = validator.Validate(instance);
        if (!result.IsValid)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, result.Errors.Select(failure => failure.ErrorMessage)));
        }
    }

    public CliApplicationBuilder ConfigureConfirmation()
    {
        _builder.Services.AddSingleton<IMigrationConfirmation, ConsoleMigrationConfirmation>();
        return this;
    }

    public NSchemaApplication Build() => _builder.Build();

    public static CliApplicationBuilder Create(NSchemaConfiguration configuration) => new(configuration);
}
