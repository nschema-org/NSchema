using Microsoft.Extensions.DependencyInjection;
using NSchema.Aws;
using NSchema.Cli.Configuration;
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
        var pattern = string.IsNullOrWhiteSpace(schema.Glob) ? schema.Format.DefaultGlob() : schema.Glob;
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
        if (_configuration.Scope is { Count: > 0 } scope)
        {
            _builder.ForSchemas([.. scope]);
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

        ThrowIfInvalid(_configuration.State.Validate());

        switch (_configuration.State)
        {
            case { File: { } file }:
                _builder.UseFileStateStore(file.Path!);
                break;
            case { S3: { } s3 }:
                _builder.UseStateStoreS3(s3.Bucket!, s3.Key!);
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

        ThrowIfInvalid(_configuration.Provider.Validate());

        switch (_configuration.Provider)
        {
            case { Postgres: { } postgres }:
                _builder.UseCurrentSchemaPostgres(dataSource =>
                {
                    dataSource.ConnectionStringBuilder.ConnectionString = postgres.ConnectionString!;
                    if (postgres.CommandTimeout is { } commandTimeout)
                    {
                        dataSource.ConnectionStringBuilder.CommandTimeout = commandTimeout;
                    }
                });
                break;
        }

        return this;
    }

    // Surfaces configuration validation failures as a single error before the run begins.
    private static void ThrowIfInvalid(IEnumerable<string> errors)
    {
        var messages = errors.ToList();
        if (messages.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, messages));
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
