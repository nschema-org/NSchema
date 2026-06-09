using Microsoft.Extensions.DependencyInjection;
using NSchema.Aws;
using NSchema.Configuration.Provider;
using NSchema.Configuration.Schema;
using NSchema.Configuration.State;
using NSchema.Diff.Policies;
using NSchema.Operations.Confirmation;
using NSchema.Postgres;
using NSchema.Schema;
using NSchema.Schema.Serialization.Ddl;
using NSchema.Services;
using Spectre.Console;

namespace NSchema;

internal sealed class CliApplicationBuilder
{
    private readonly NSchemaApplicationBuilder _builder = NSchemaApplication
        .CreateBuilder(new NSchemaApplicationOptions
        {
            ExceptionBehavior = ExceptionBehavior.Throw,
            Reporter = SpectreOperationReporter.ReporterName
        })
        // Render the diff as plain text so the Spectre reporter owns all color; otherwise the core's raw ANSI
        // would be re-escaped by Spectre. The reporter then frames and colors it.
        .UseTerraformRenderer(o => o.IncludeColour = false)
        .AddReporter<SpectreOperationReporter>(SpectreOperationReporter.ReporterName);

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
        var pattern = string.IsNullOrWhiteSpace(schema.Pattern) ? "**/*.sql" : schema.Pattern;
        var glob = $"{root}/{pattern}";

        _builder.AddFileSchemasFromGlob(glob, path => new FileSchemaProvider(path, DdlSchemaSerializer.Instance));

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

    public CliApplicationBuilder ConfigureConfirmation(bool autoApprove)
    {
        _builder.Services.AddSingleton<IOperationConfirmation>(sp => new ConsoleOperationConfirmation(autoApprove, sp.GetRequiredService<IAnsiConsole>()));
        return this;
    }

    public NSchemaApplication Build() => _builder.Build();

    public static CliApplicationBuilder Create() => new();
}
