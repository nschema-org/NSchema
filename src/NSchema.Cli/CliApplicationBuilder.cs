using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using NSchema.Aws;
using NSchema.Cli.Configuration.Import;
using NSchema.Cli.Configuration.Provider;
using NSchema.Cli.Configuration.Schema;
using NSchema.Cli.Configuration.State;
using NSchema.Cli.Services;
using NSchema.Hosting;
using NSchema.Migration;
using NSchema.Postgres;
using NSchema.Yaml;
using Spectre.Console;

namespace NSchema.Cli;

internal sealed class CliApplicationBuilder
{
    private readonly NSchemaApplicationBuilder _builder = NSchemaApplication.CreateBuilder()
        .WithExceptionBehavior(ExceptionBehavior.Throw)
        .AddYamlSchemaSerializer()
        // Render the diff as plain text so the Spectre reporter owns all color; otherwise the core's raw ANSI
        // would be re-escaped by Spectre. The reporter then frames and colors it.
        .UseTerraformRenderer(o => o.IncludeColour = false)
        .AddReporter<SpectreMigrationReporter>(SpectreMigrationReporter.FormatName)
        .WithOutputFormat(SpectreMigrationReporter.FormatName);

    private CliApplicationBuilder()
    {
        _builder.Services.AddSingleton(AnsiConsole.Console);
    }

    public CliApplicationBuilder ConfigurePolicies(DestructiveActionPolicy? policy)
    {
        if (policy is { } value)
        {
            _builder.WithDestructiveActionPolicy(value);
        }

        return this;
    }

    public CliApplicationBuilder ConfigureDesiredSchema(SchemaConfig schema)
    {
        var root = Path.GetFullPath(schema.Directory, Directory.GetCurrentDirectory());
        var pattern = string.IsNullOrWhiteSpace(schema.Pattern) ? schema.Format.DefaultPattern() : schema.Pattern;
        var glob = $"{root}/{pattern}";

        switch (schema.Format)
        {
            case SchemaFormat.Yaml: _builder.AddYamlSchemasFromGlob(glob); break;
            case SchemaFormat.Json: _builder.AddJsonSchemasFromGlob(glob); break;
            default: throw new UnreachableException();
        }

        return this;
    }

    public CliApplicationBuilder ConfigureScope(string[]? scope)
    {
        if (scope is { Length: > 0 })
        {
            _builder.ForSchemas(scope);
        }

        return this;
    }

    public CliApplicationBuilder ConfigureBackendState(StateConfig state)
    {
        switch (state)
        {
            case { File: { } file }:
                _builder.UseFileStateStore(file.Path);
                break;
            case { S3: { } s3 }:
                _builder.UseS3StateStore(s3.Bucket, s3.Key);
                break;
        }

        return this;
    }

    public CliApplicationBuilder ConfigureDatabaseProvider(ProviderConfig provider)
    {
        // The property pattern binds non-null locals; the command validator guarantees it matches when required.
        if (provider is { Postgres: { } postgres })
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

    public CliApplicationBuilder ConfigureImportTarget(ImportTargetConfig importTarget)
    {
        _builder.AddFileImportTarget(o =>
        {
            o.OutputPath = Path.GetFullPath(importTarget.OutputPath, Directory.GetCurrentDirectory());
            o.Format = importTarget.Format.FormatName();
            o.Partition = importTarget.Partition;
        });
        return this;
    }

    public CliApplicationBuilder ConfigureImportScope(string[]? schemas, string[]? tables)
    {
        _builder.WithImportOptions(o =>
        {
            o.Schemas = schemas;
            o.Tables = tables;
        });
        return this;
    }

    public CliApplicationBuilder ConfigureConfirmation(bool autoApprove)
    {
        _builder.Services.AddSingleton<IMigrationConfirmation>(sp => new ConsoleMigrationConfirmation(autoApprove, sp.GetRequiredService<IAnsiConsole>()));
        return this;
    }

    public NSchemaApplication Build() => _builder.Build();

    public static CliApplicationBuilder Create() => new();
}
