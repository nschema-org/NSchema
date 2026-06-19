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

    private CliApplicationBuilder(bool json, Verbosity verbosity)
    {
        _builder = NSchemaApplication.CreateBuilder(new NSchemaApplicationOptions
        {
            ExceptionBehavior = ExceptionBehavior.Throw,
        });

        _builder.UseTerraformRenderer(o => o.IncludeColour = false);

        if (json)
        {
            _builder.UseReporter<JsonOperationReporter>();
        }
        else
        {
            _builder.UseReporter<SpectreOperationReporter>();
        }

        _builder.Services.AddSingleton(AnsiConsole.Console);
        _builder.Services.AddSingleton<RunOutcome>();
        _builder.Services.AddSingleton(new OutputVerbosity(verbosity));
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

    /// <summary>Creates a builder rendering formatted (text) output at the default verbosity.</summary>
    public static CliApplicationBuilder Create() => new(json: false, Verbosity.Normal);

    /// <summary>Creates a builder whose output format and verbosity follow the command-line flags.</summary>
    public static CliApplicationBuilder Create(ParseResult parseResult) =>
        new(CommonOptions.Json.GetValueOrDefault(null, parseResult, false), ResolveVerbosity(parseResult));

    /// <summary>
    /// Resolves <c>--quiet</c> / <c>--verbose</c> to a single <see cref="Verbosity"/>. The two flags are mutually
    /// exclusive: passing both is a usage error rather than a silent precedence.
    /// </summary>
    private static Verbosity ResolveVerbosity(ParseResult parseResult)
    {
        var quiet = CommonOptions.Quiet.GetValueOrDefault(null, parseResult, false);
        var verbose = CommonOptions.Verbose.GetValueOrDefault(null, parseResult, false);

        if (quiet && verbose)
        {
            throw new InvalidOperationException("--quiet and --verbose cannot be used together.");
        }

        return verbose ? Verbosity.Verbose : quiet ? Verbosity.Quiet : Verbosity.Normal;
    }
}
