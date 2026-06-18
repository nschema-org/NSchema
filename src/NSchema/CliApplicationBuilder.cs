using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using NSchema.Aws;
using NSchema.Configuration;
using NSchema.Configuration.Provider;
using NSchema.Configuration.State;
using NSchema.Diff.Policies;
using NSchema.Operations.Confirmation;
using NSchema.Postgres;
using NSchema.Services;
using Spectre.Console;

namespace NSchema;

internal sealed class CliApplicationBuilder
{
    private readonly NSchemaApplicationBuilder _builder;

    private CliApplicationBuilder(bool json)
    {
        _builder = NSchemaApplication.CreateBuilder(new NSchemaApplicationOptions
        {
            ExceptionBehavior = ExceptionBehavior.Throw,
            Reporter = json ? JsonOperationReporter.ReporterName : SpectreOperationReporter.ReporterName,
        });

        _builder
            .UseTerraformRenderer(o => o.IncludeColour = false)
            .AddReporter<SpectreOperationReporter>(SpectreOperationReporter.ReporterName)
            .AddReporter<JsonOperationReporter>(JsonOperationReporter.ReporterName);

        _builder.Services.AddSingleton(AnsiConsole.Console);
        _builder.Services.AddSingleton<RunOutcome>();
    }

    public CliApplicationBuilder ConfigurePolicies(DestructiveActionPolicy? policy)
    {
        if (policy is { } value)
        {
            _builder.WithDestructiveActionPolicy(value);
        }

        return this;
    }

    public CliApplicationBuilder ConfigureDesiredSchema(string? environment)
    {
        var root = Directory.GetCurrentDirectory();
        _builder.AddDdlSchemas(root, ProjectGlobs.BaseSchema());

        // Layer the selected environment's overlay files on top.
        if (environment is not null)
        {
            _builder.AddDdlSchemas(root, ProjectGlobs.EnvironmentSchema(environment));
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

    public CliApplicationBuilder ConfigureConfirmation(bool autoApprove)
    {
        _builder.Services.AddSingleton<IOperationConfirmation>(sp => new ConsoleOperationConfirmation(autoApprove, sp.GetRequiredService<IAnsiConsole>()));
        return this;
    }

    public NSchemaApplication Build() => _builder.Build();

    /// <summary>Creates a builder rendering formatted (text) output.</summary>
    public static CliApplicationBuilder Create() => new(json: false);

    /// <summary>Creates a builder whose output format follows the <c>--json</c> flag on the command line.</summary>
    public static CliApplicationBuilder Create(ParseResult parseResult) =>
        new(CommonOptions.Json.GetValueOrDefault(null, parseResult, false));
}
