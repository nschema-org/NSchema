using NSchema.Configuration;

namespace NSchema.Tests.Configuration;

public sealed class ProjectConfigReaderTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("nschema-cfg-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private async Task<ProjectConfig> Read(string sql)
    {
        await File.WriteAllTextAsync(Path.Combine(_directory, "config.env.sql"), sql, TestContext.Current.CancellationToken);
        return await ProjectConfigReader.Read(_directory, environment: null, TestContext.Current.CancellationToken);
    }

    private async Task<ProjectConfig> ReadEnvironment(string baseSql, string environment, string overlaySql)
    {
        await File.WriteAllTextAsync(Path.Combine(_directory, "config.env.sql"), baseSql, TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(_directory, $"config.env.{environment}.sql"), overlaySql, TestContext.Current.CancellationToken);
        return await ProjectConfigReader.Read(_directory, environment, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Database_ResolvesDeclaredPlugin()
    {
        var config = await Read(
            """
            PLUGIN postgres ( source = 'NSchema.Postgres', version = '5.0.0' );
            DATABASE postgres ( connection_string = 'host=db' );
            """);

        var plugin = config.Provider!;
        plugin.PackageId.ShouldBe("NSchema.Postgres");
        plugin.Label.ShouldBe("postgres");
        plugin.Version.ShouldBe("5.0.0");
        // The plugin's own attributes ride the statement's config for the plugin to interpret.
        plugin.Config.Attribute("connection_string")!.AsString().ShouldBe("host=db");
    }

    [Fact]
    public async Task Database_LabelIsLocal_SourceNamesAnyPackage()
    {
        var config = await Read(
            """
            PLUGIN oracle ( source = 'Acme.NSchema.Oracle', version = '1.0.0' );
            DATABASE oracle ( connection_string = 'x' );
            """);

        config.Provider!.PackageId.ShouldBe("Acme.NSchema.Oracle");
        config.Provider!.Label.ShouldBe("oracle");
    }

    [Fact]
    public async Task Database_UndeclaredLabel_Throws()
        => (await Should.ThrowAsync<InvalidOperationException>(() => Read("DATABASE postgres ( connection_string = 'x' );")))
            .Message.ShouldContain("no PLUGIN statement declares it");

    [Fact]
    public async Task Plugin_MissingVersion_Throws()
        => (await Should.ThrowAsync<InvalidOperationException>(() => Read("PLUGIN postgres ( source = 'NSchema.Postgres' );")))
            .Message.ShouldContain("version");

    [Fact]
    public async Task Plugin_ExactPin_KeepsBareVersionForDisplayAndIntervalForRestore()
    {
        var config = await Read(
            """
            PLUGIN postgres ( source = 'NSchema.Postgres', version = '5.0.0-alpha.1' );
            DATABASE postgres ( connection_string = 'x' );
            """);

        config.Provider!.Version.ShouldBe("5.0.0-alpha.1");
        config.Provider!.RestoreVersion.ShouldBe("[5.0.0-alpha.1]");
    }

    [Fact]
    public async Task Plugin_Range_KeepsIntervalNotation()
    {
        var config = await Read(
            """
            PLUGIN postgres ( source = 'NSchema.Postgres', version = '[5.0,6.0)' );
            DATABASE postgres ( connection_string = 'x' );
            """);

        config.Provider!.Version.ShouldBe("[5.0.0,6.0.0)");
        config.Provider!.RestoreVersion.ShouldBe("[5.0.0,6.0.0)");
    }

    [Fact]
    public async Task State_File_MapsPath()
        => (await Read("STATE file ( path = './state.json' );")).State!.File!.Path.ShouldBe("./state.json");

    [Fact]
    public async Task State_S3_ResolvesDeclaredPlugin()
    {
        var plugin = (await Read(
            """
            PLUGIN s3 ( source = 'NSchema.Aws', version = '5.0.0' );
            STATE s3 ( bucket = 'my-bucket', key = 'state.json' );
            """)).State!.Plugin!;

        plugin.PackageId.ShouldBe("NSchema.Aws");
        plugin.Label.ShouldBe("s3");
        plugin.Config.Attribute("bucket")!.AsString().ShouldBe("my-bucket");
    }

    [Fact]
    public async Task State_File_UnknownAttribute_Throws()
        // The built-in file store parses its own attributes; other backends defer attribute validation to the plugin.
        => (await Should.ThrowAsync<InvalidOperationException>(() => Read("STATE file ( badly = 'x' );")))
            .Message.ShouldContain("Unknown attribute");

    [Fact]
    public async Task Engine_SatisfiedAssertion_Passes()
    {
        var config = await Read("ENGINE ( version = '[5.0,6.0)' );");

        config.Provider.ShouldBeNull();
        config.State.ShouldBeNull();
    }

    [Fact]
    public async Task Engine_UnsatisfiedAssertion_Throws()
        => (await Should.ThrowAsync<InvalidOperationException>(() => Read("ENGINE ( version = '[4.0,5.0)' );")))
            .Message.ShouldContain("requires an engine version");

    [Fact]
    public async Task NoConfigFile_ReturnsEmpty()
    {
        await File.WriteAllTextAsync(Path.Combine(_directory, "schema.sql"), "CREATE SCHEMA app;", TestContext.Current.CancellationToken);

        var config = await ProjectConfigReader.Read(_directory, environment: null, TestContext.Current.CancellationToken);

        config.Provider.ShouldBeNull();
        config.State.ShouldBeNull();
    }

    [Fact]
    public async Task MultipleBaseConfigFiles_AllLoad()
    {
        // The .env. marker is a pattern, not a fixed name: every *.env.sql file contributes to the base layer.
        await File.WriteAllTextAsync(Path.Combine(_directory, "plugins.env.sql"),
            "PLUGIN postgres ( source = 'NSchema.Postgres', version = '5.0.0' );", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(_directory, "database.env.sql"),
            "DATABASE postgres ( connection_string = 'host=db' );", TestContext.Current.CancellationToken);

        var config = await ProjectConfigReader.Read(_directory, environment: null, TestContext.Current.CancellationToken);

        config.Provider!.PackageId.ShouldBe("NSchema.Postgres");
    }

    [Fact]
    public async Task SchemaStatementInConfigFile_Throws()
        // A configuration file holds only configuration statements; DDL belongs in the schema files.
        => await Should.ThrowAsync<InvalidOperationException>(() => Read("CREATE SCHEMA app;"));

    [Fact]
    public async Task DuplicateDatabase_Throws()
        => (await Should.ThrowAsync<InvalidOperationException>(() =>
                Read(
                    """
                    PLUGIN postgres ( source = 'NSchema.Postgres', version = '5.0.0' );
                    DATABASE postgres ( a = 'x' );
                    DATABASE postgres ( a = 'y' );
                    """)))
            .Message.ShouldContain("at most one DATABASE");

    // ── Environment overlays ──────────────────────────────────────────────────

    [Fact]
    public async Task Environment_OverlayReplacesBaseSlice()
    {
        // The base uses the file store; the prod overlay switches it to S3, replacing the slice wholesale. The
        // PLUGIN declaration is project-wide, so it lives in the base even though only the overlay references it.
        var config = await ReadEnvironment(
            """
            PLUGIN s3 ( source = 'NSchema.Aws', version = '5.0.0' );
            STATE file ( path = './state.json' );
            """,
            "prod",
            "STATE s3 ( bucket = 'prod-bucket', key = 'state.json' );");

        config.State!.File.ShouldBeNull();
        config.State.Plugin!.Config.Attribute("bucket")!.AsString().ShouldBe("prod-bucket");
    }

    [Fact]
    public async Task Environment_BaseSliceSurvivesWhenOverlayOmitsIt()
    {
        // The overlay only declares a state store; the base database carries through unchanged.
        var config = await ReadEnvironment(
            """
            PLUGIN postgres ( source = 'NSchema.Postgres', version = '5.0.0' );
            DATABASE postgres ( connection_string = 'host=base' );
            """,
            "prod",
            "STATE file ( path = './prod.state.json' );");

        config.Provider!.Config.Attribute("connection_string")!.AsString().ShouldBe("host=base");
        config.State!.File!.Path.ShouldBe("./prod.state.json");
    }

    [Fact]
    public async Task Environment_NotFound_Throws()
    {
        await File.WriteAllTextAsync(Path.Combine(_directory, "config.env.sql"), "STATE file ( path = './state.json' );", TestContext.Current.CancellationToken);

        (await Should.ThrowAsync<InvalidOperationException>(
                () => ProjectConfigReader.Read(_directory, "prod", TestContext.Current.CancellationToken).AsTask()))
            .Message.ShouldContain("environment 'prod'");
    }

    [Fact]
    public async Task Environment_Null_IgnoresOverlayFiles()
    {
        // An overlay file is present, but with no environment selected the base config must not read its statements.
        await File.WriteAllTextAsync(Path.Combine(_directory, "config.env.prod.sql"),
            "STATE file ( path = './prod.state.json' );", TestContext.Current.CancellationToken);

        var config = await Read("ENGINE ( version = '[5.0,6.0)' );");

        config.State.ShouldBeNull();
    }
}
