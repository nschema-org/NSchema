using Microsoft.Extensions.DependencyInjection;
using NSchema.Cli.Configuration;
using NSchema.Cli.Services;
using NSchema.Hosting;
using NSchema.Yaml;

namespace NSchema.Cli;

/// <summary>
/// Translates resolved <see cref="NSchemaConfiguration"/> into a configured <see cref="NSchemaApplication"/>.
/// </summary>
internal class CliApplicationBuilder
{
    private readonly NSchemaConfiguration _configuration;

    private readonly NSchemaApplicationBuilder _builder = NSchemaApplication.CreateBuilder();

    private CliApplicationBuilder(NSchemaConfiguration configuration)
    {
        _configuration = configuration;
        _builder.WithExceptionBehavior(ExceptionBehavior.Throw);
        _builder.Services.AddSingleton(configuration);
        _builder.Services.AddSingleton<IMigrationConfirmation, ConsoleMigrationConfirmation>();
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
        // TODO: Add AWS S3 support.
        if (_configuration.State.File is { } stateFile)
        {
            _builder.UseFileStateStore(stateFile);
        }
        return this;
    }

    public CliApplicationBuilder ConfigureDatabaseProvider()
    {
        // TODO: Add postgres support.
        if (_configuration.Provider is { Length: > 0 } provider)
        {
            throw new NotSupportedException(
                $"Database provider '{provider}' is not available in this build. No database providers are bundled yet, " +
                "so only offline operations (plan/refresh against a state file) are supported.");
        }
        return this;
    }

    public NSchemaApplication Build() => _builder.Build();

    public static CliApplicationBuilder Create(NSchemaConfiguration configuration) => new(configuration);
}
