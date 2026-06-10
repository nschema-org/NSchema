using Microsoft.Extensions.DependencyInjection;
using NSchema.Aws;
using NSchema.Configuration.Provider;
using NSchema.Configuration.State;
using NSchema.Diff.Policies;
using NSchema.Operations.Confirmation;
using NSchema.Postgres;
using NSchema.Schema;
using NSchema.Schema.Serialization.Ddl;
using NSchema.Scripts.Model;
using NSchema.Services;
using Spectre.Console;

namespace NSchema;

internal sealed class CliApplicationBuilder
{

    private const string PreScriptSuffix = ".pre.sql";
    private const string PostScriptSuffix = ".post.sql";
    private static readonly EnumerationOptions _sqlEnumeration = new() { RecurseSubdirectories = true, IgnoreInaccessible = true };

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

    public CliApplicationBuilder ConfigureDesiredSchema()
    {
        var root = Directory.GetCurrentDirectory();
        var schemaFiles = SqlFiles(root).Where(IsSchemaFile).ToList();

        // Guard the empty-glob footgun: with no schema files the desired schema would be empty, and a forward plan or
        // apply would read that as "drop everything". A clear error beats a surprising teardown.
        if (schemaFiles.Count == 0)
        {
            throw new InvalidOperationException(
                $"No schema files (*.sql) found under \"{root}\". Add a .sql schema file, or point at your project directory with --directory.");
        }

        foreach (var file in schemaFiles)
        {
            _builder.AddSchema(_ => new FileSchemaProvider(file, DdlSchemaSerializer.Instance));
        }

        return this;
    }

    public CliApplicationBuilder ConfigureScripts()
    {
        foreach (var file in SqlFiles(Directory.GetCurrentDirectory()))
        {
            if (file.EndsWith(PreScriptSuffix, StringComparison.OrdinalIgnoreCase))
            {
                _builder.AddScriptFromFile(ScriptType.PreDeployment, file, ScriptName(file, PreScriptSuffix));
            }
            else if (file.EndsWith(PostScriptSuffix, StringComparison.OrdinalIgnoreCase))
            {
                _builder.AddScriptFromFile(ScriptType.PostDeployment, file, ScriptName(file, PostScriptSuffix));
            }
        }

        return this;
    }

    private static List<string> SqlFiles(string root) =>
        Directory.EnumerateFiles(root, "*.sql", _sqlEnumeration).OrderBy(path => path, StringComparer.Ordinal).ToList();

    // The plan/log name drops the whole .pre.sql/.post.sql suffix, so "001_extensions.pre.sql" reads as "001_extensions".
    private static string ScriptName(string path, string suffix)
    {
        var fileName = Path.GetFileName(path);
        return fileName[..^suffix.Length];
    }

    private static bool IsSchemaFile(string path) =>
        !path.EndsWith(PreScriptSuffix, StringComparison.OrdinalIgnoreCase) &&
        !path.EndsWith(PostScriptSuffix, StringComparison.OrdinalIgnoreCase);

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
