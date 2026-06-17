using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSchema.Configuration.State;
using NSchema.Diff.Policies;
using NSchema.Operations;
using NSchema.Resolution;
using NSchema.Schema;
using NSchema.Schema.Model;
using NSchema.Scripts;
using NSchema.Scripts.Model;
using NSchema.Services;
using NSchema.State;
using Spectre.Console;

namespace NSchema.Tests;

public sealed class CliApplicationBuilderTests
{
    private readonly CliApplicationBuilder _sut = CliApplicationBuilder.Create();

    [Fact]
    public void ConfigurePolicies_AppliesDestructiveActionPolicy()
    {
        // Act
        using var app = _sut.ConfigurePolicies(DestructiveActionPolicy.Warn).Build();

        // Assert
        var options = app.Services.GetRequiredService<IOptions<DestructiveActionOptions>>().Value;
        options.Policy.ShouldBe(DestructiveActionPolicy.Warn);
    }

    [Fact]
    public void ConfigurePolicies_LeavesDefault_WhenPolicyNull()
    {
        // Act
        using var app = _sut.ConfigurePolicies(null).Build();

        // Assert
        var options = app.Services.GetRequiredService<IOptions<DestructiveActionOptions>>().Value;
        options.Policy.ShouldBe(DestructiveActionPolicy.Error);
    }

    [Fact]
    public void ConfigureBackendState_RegistersStateStore_ForFile()
    {
        // Arrange
        var state = new StateConfig { File = new FileStateConfig { Path = "./state.json" } };

        // Act
        using var app = _sut.ConfigureBackendState(state).Build();

        // Assert
        app.Services.GetService<ISchemaStateStore>().ShouldNotBeNull();
    }

    [Fact]
    public void ConfigureBackendState_RegistersNoStateStore_WhenNoStoreConfigured()
    {
        // Act
        using var app = _sut.ConfigureBackendState(new StateConfig()).Build();

        // Assert
        app.Services.GetService<ISchemaStateStore>().ShouldBeNull();
    }

    [Fact]
    public void Build_RegistersAnAnsiConsole()
    {
        // Act
        using var app = _sut.Build();

        // Assert
        app.Services.GetService<IAnsiConsole>().ShouldNotBeNull();
    }

    [Fact]
    public void Build_ResolvesTheSpectreReporter_WithoutDuplicateFormatCollision()
    {
        // Act — a second reporter sharing the core "human" format would throw at resolution; the Spectre reporter
        // is registered under its own format and selected, so the default coexists harmlessly.
        using var app = _sut.Build();

        // Assert
        var reporter = app.Services.GetRequiredService<IKeyedResolver<IOperationReporter>>().Current;
        reporter.ShouldBeOfType<SpectreOperationReporter>();
    }

    [Fact]
    public async Task ConfigureDesiredSchema_FindsSqlFilesRecursively()
    {
        var original = Directory.GetCurrentDirectory();
        var directory = Directory.CreateTempSubdirectory("nschema-schema-").FullName;
        try
        {
            var nested = Directory.CreateDirectory(Path.Combine(directory, "schemas"));
            File.WriteAllText(Path.Combine(nested.FullName, "example.sql"), "CREATE SCHEMA app;");
            Directory.SetCurrentDirectory(directory);

            using var app = _sut.ConfigureDesiredSchema(null).Build();

            var schema = await ResolveDesiredSchema(app);
            schema.Schemas.ShouldHaveSingleItem().Name.ShouldBe("app");
        }
        finally
        {
            Directory.SetCurrentDirectory(original);
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ConfigureDesiredSchema_ExcludesDeploymentScripts()
    {
        var original = Directory.GetCurrentDirectory();
        var directory = Directory.CreateTempSubdirectory("nschema-onlyscripts-").FullName;
        try
        {
            // Only deployment scripts, no declarative schema. The exclude globs must keep them out of the desired
            // schema, leaving nothing to resolve — so the core schema provider reports no matching DDL files rather
            // than treating a script as schema. (An empty desired schema would otherwise read as "drop everything".)
            File.WriteAllText(Path.Combine(directory, "001_extensions.pre.sql"), "CREATE EXTENSION IF NOT EXISTS citext;");
            File.WriteAllText(Path.Combine(directory, "010_backfill.post.sql"), "UPDATE app.widgets SET name = '';");
            Directory.SetCurrentDirectory(directory);

            using var app = _sut.ConfigureDesiredSchema(null).Build();

            var act = () => ResolveDesiredSchema(app);

            (await Should.ThrowAsync<FileNotFoundException>(act)).Message.ShouldContain("No SQL DDL files matched");
        }
        finally
        {
            Directory.SetCurrentDirectory(original);
            Directory.Delete(directory, recursive: true);
        }
    }

    // Resolves the aggregated desired schema the way the core does at run time. With only ConfigureDesiredSchema
    // applied, the single registered provider is the DDL glob provider, so this exercises the matcher's selection.
    private static async Task<DatabaseSchema> ResolveDesiredSchema(NSchemaApplication app)
    {
        var provider = app.Services.GetServices<ISchemaProvider>().ShouldHaveSingleItem();
        return await provider.GetSchema([], TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ConfigureScripts_RegistersPreAndPostDeploymentScriptsBySuffix()
    {
        var original = Directory.GetCurrentDirectory();
        var directory = Directory.CreateTempSubdirectory("nschema-scripts-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(directory, "schema.sql"), "CREATE SCHEMA app;");
            File.WriteAllText(Path.Combine(directory, "001_extensions.pre.sql"), "CREATE EXTENSION IF NOT EXISTS citext;");
            File.WriteAllText(Path.Combine(directory, "010_backfill.post.sql"), "UPDATE app.widgets SET name = '';");
            Directory.SetCurrentDirectory(directory);

            using var app = _sut.ConfigureDesiredSchema(null).ConfigureScripts().Build();

            var scripts = new List<Script>();
            foreach (var provider in app.Services.GetServices<IScriptProvider>())
            {
                scripts.AddRange(await provider.GetScripts(TestContext.Current.CancellationToken));
            }

            // The script name is core-supplied: the file name as-is, including the extension.
            scripts.Select(s => (s.Name, s.Type)).ShouldBe(
                [("001_extensions.pre.sql", ScriptType.PreDeployment), ("010_backfill.post.sql", ScriptType.PostDeployment)],
                ignoreOrder: true);
        }
        finally
        {
            Directory.SetCurrentDirectory(original);
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ConfigureDesiredSchema_WithEnvironment_LayersOverlayOverBase()
    {
        var original = Directory.GetCurrentDirectory();
        var directory = Directory.CreateTempSubdirectory("nschema-env-").FullName;
        try
        {
            // Base schema, plus a prod overlay adding an object. The plain base file is read; the overlay is only
            // pulled in because prod is selected. A dev overlay must stay out.
            File.WriteAllText(Path.Combine(directory, "schema.sql"), "CREATE SCHEMA app;");
            File.WriteAllText(Path.Combine(directory, "audit.env.prod.sql"), "CREATE SCHEMA audit;");
            File.WriteAllText(Path.Combine(directory, "scratch.env.dev.sql"), "CREATE SCHEMA scratch;");
            Directory.SetCurrentDirectory(directory);

            using var app = _sut.ConfigureDesiredSchema("prod").Build();

            // Two providers now: the base glob and the prod overlay glob. Their schemas combine.
            var providers = app.Services.GetServices<ISchemaProvider>();
            var names = new List<string>();
            foreach (var provider in providers)
            {
                names.AddRange((await provider.GetSchema([], TestContext.Current.CancellationToken)).Schemas.Select(s => s.Name));
            }

            names.ShouldBe(["app", "audit"], ignoreOrder: true);
        }
        finally
        {
            Directory.SetCurrentDirectory(original);
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ConfigureScripts_ExcludesEnvironmentOverlays()
    {
        var original = Directory.GetCurrentDirectory();
        var directory = Directory.CreateTempSubdirectory("nschema-envscripts-").FullName;
        try
        {
            // A normal pre-script is picked up; an env-marked file that happens to end in .pre.sql must not leak in
            // as a global script (it would otherwise run in every environment).
            File.WriteAllText(Path.Combine(directory, "schema.sql"), "CREATE SCHEMA app;");
            File.WriteAllText(Path.Combine(directory, "001_extensions.pre.sql"), "CREATE EXTENSION IF NOT EXISTS citext;");
            File.WriteAllText(Path.Combine(directory, "seed.env.prod.pre.sql"), "INSERT INTO app.t VALUES (1);");
            Directory.SetCurrentDirectory(directory);

            using var app = _sut.ConfigureDesiredSchema(null).ConfigureScripts().Build();

            var scripts = new List<Script>();
            foreach (var provider in app.Services.GetServices<IScriptProvider>())
            {
                scripts.AddRange(await provider.GetScripts(TestContext.Current.CancellationToken));
            }

            scripts.Select(s => s.Name).ShouldBe(["001_extensions.pre.sql"]);
        }
        finally
        {
            Directory.SetCurrentDirectory(original);
            Directory.Delete(directory, recursive: true);
        }
    }
}
