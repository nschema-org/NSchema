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
        if (_configuration.Scope.Count > 0)
        {
            _builder.ForSchemas([.. _configuration.Scope]);
        }
        return this;
    }

    public CliApplicationBuilder ConfigureBackendState()
    {
        // No connection string means no state store: plan/apply run online-only and refresh has nowhere to write.
        if (string.IsNullOrWhiteSpace(_configuration.State.ConnectionString))
        {
            return this;
        }

        var connectionString = _configuration.State.ConnectionString;
        switch (_configuration.State.Type ?? StateType.File)
        {
            case StateType.File:
                _builder.UseFileStateStore(connectionString);
                break;
            case StateType.S3:
                var (bucket, key) = ParseS3ConnectionString(connectionString);
                _builder.UseStateStoreS3(bucket, key);
                break;
            default:
                throw new NotSupportedException($"Unknown state store type '{_configuration.State.Type}'.");
        }

        return this;
    }

    public CliApplicationBuilder ConfigureDatabaseProvider()
    {
        switch (_configuration.Provider.Type)
        {
            // No provider configured: only offline operations (plan/refresh against the state store) are available.
            case null:
                break;
            case ProviderType.Postgres:
                _builder.UseCurrentSchemaPostgres(RequireConnectionString("postgres"));
                break;
            default:
                throw new NotSupportedException($"Unknown database provider '{_configuration.Provider.Type}'.");
        }

        return this;
    }

    private string RequireConnectionString(string provider)
    {
        if (string.IsNullOrWhiteSpace(_configuration.Provider.ConnectionString))
        {
            throw new InvalidOperationException(
                $"The '{provider}' provider requires a connection string. Set it via --connection-string, " +
                "NSCHEMA_CONNECTION_STRING, or nschema.json.");
        }

        return _configuration.Provider.ConnectionString;
    }

    // A state store of type s3 is addressed by an s3://bucket/key connection string.
    private static (string Bucket, string Key) ParseS3ConnectionString(string connectionString)
    {
        const string scheme = "s3://";
        if (connectionString.StartsWith(scheme, StringComparison.OrdinalIgnoreCase))
        {
            var path = connectionString[scheme.Length..];
            var separator = path.IndexOf('/');
            if (separator > 0 && separator < path.Length - 1)
            {
                return (path[..separator], path[(separator + 1)..]);
            }
        }

        throw new InvalidOperationException($"Invalid s3 state store connection string '{connectionString}'. Expected the form 's3://bucket/key'.");
    }

    public CliApplicationBuilder ConfigureConfirmation()
    {
        _builder.Services.AddSingleton<IMigrationConfirmation, ConsoleMigrationConfirmation>();
        return this;
    }

    public NSchemaApplication Build() => _builder.Build();

    public static CliApplicationBuilder Create(NSchemaConfiguration configuration) => new(configuration);
}
