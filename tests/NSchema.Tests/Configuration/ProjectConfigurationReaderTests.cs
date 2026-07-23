using NSchema.Configuration;
using NSchema.Configuration.Model;
using NSchema.Configuration.Plugins;

namespace NSchema.Tests.Configuration;

public sealed class ProjectConfigurationReaderTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("nschema-cfg-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private async Task<ProjectConfiguration> Read(string sql)
    {
        await File.WriteAllTextAsync(Path.Combine(_directory, "config.env.sql"), sql, TestContext.Current.CancellationToken);
        return await ProjectConfigurationReader.Read(_directory, environment: null, TestContext.Current.CancellationToken);
    }

    private Task WriteLock(params LockedPlugin[] plugins) =>
        LockFileManager.Write(ProjectConfigurationReader.LockFilePath(_directory), new LockFile(plugins), TestContext.Current.CancellationToken);

    private static LockedPlugin Locked(string source, string version) =>
        new() { Source = new PackageId(source), Version = SemanticVersion.Parse(version) };

    private async Task<ProjectConfiguration> ReadEnvironment(string baseSql, string environment, string overlaySql)
    {
        await File.WriteAllTextAsync(Path.Combine(_directory, "config.env.sql"), baseSql, TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(_directory, $"config.env.{environment}.sql"), overlaySql, TestContext.Current.CancellationToken);
        return await ProjectConfigurationReader.Read(_directory, environment, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Database_ResolvesDeclaredPlugin()
    {
        await WriteLock(Locked("NSchema.Postgres", "5.0.0"));
        var config = await Read(
            """
            PLUGIN postgres ( source = 'NSchema.Postgres', version = '5.0.0' );
            DATABASE postgres ( connection_string = 'host=db' );
            """);

        var plugin = config.Database!;
        plugin.PackageId.ShouldBe("NSchema.Postgres");
        plugin.Label.ShouldBe("postgres");
        plugin.Version.ToString().ShouldBe("5.0.0");
        // The plugin's own attributes ride the statement's config for the plugin to interpret.
        plugin.Settings.Attribute("connection_string")!.ShouldBe("host=db");
    }

    [Fact]
    public async Task Database_LabelIsLocal_SourceNamesAnyPackage()
    {
        await WriteLock(Locked("Acme.NSchema.Oracle", "1.0.0"));
        var config = await Read(
            """
            PLUGIN oracle ( source = 'Acme.NSchema.Oracle', version = '1.0.0' );
            DATABASE oracle ( connection_string = 'x' );
            """);

        config.Database!.PackageId.ShouldBe("Acme.NSchema.Oracle");
        config.Database!.Label.ShouldBe("oracle");
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
    public async Task Plugin_ExactPin_ResolvesLockedVersion()
    {
        await WriteLock(Locked("NSchema.Postgres", "5.0.0-alpha.2"));
        var config = await Read(
            """
            PLUGIN postgres ( source = 'NSchema.Postgres', version = '5.0.0-alpha.2' );
            DATABASE postgres ( connection_string = 'x' );
            """);

        config.Database!.Version.ToString().ShouldBe("5.0.0-alpha.2");
    }

    [Fact]
    public async Task Plugin_WithoutLock_ThrowsAndSuggestsInit()
        // Every plugin resolves from the lockfile — an exact pin needs a lock entry just like a range.
        => (await Should.ThrowAsync<InvalidOperationException>(() => Read(
                """
                PLUGIN postgres ( source = 'NSchema.Postgres', version = '5.0.0' );
                DATABASE postgres ( connection_string = 'x' );
                """)))
            .Message.ShouldContain("nschema init");

    [Fact]
    public async Task Plugin_Range_ResolvesLockedVersion()
    {
        await WriteLock(new LockedPlugin { Source = new PackageId("NSchema.Postgres"), Version = new SemanticVersion(5, 3, 1, 0, null) });

        var config = await Read(
            """
            PLUGIN postgres ( source = 'NSchema.Postgres', version = '[5.0,6.0)' );
            DATABASE postgres ( connection_string = 'x' );
            """);

        // The range pins to the locked version; that pin is both the display/cache key and (as an interval) the restore.
        config.Database!.Version.ToString().ShouldBe("5.3.1");
    }

    [Fact]
    public async Task State_File_MapsPath()
        => (await Read("STATE file ( path = './state.json' );")).State!.File!.Path.ShouldBe("./state.json");

    [Fact]
    public async Task State_S3_ResolvesDeclaredPlugin()
    {
        await WriteLock(Locked("NSchema.Aws", "5.0.0"));
        var plugin = (await Read(
            """
            PLUGIN s3 ( source = 'NSchema.Aws', version = '5.0.0' );
            STATE s3 ( bucket = 'my-bucket', key = 'state.json' );
            """)).State!.Plugin!;

        plugin.PackageId.ShouldBe("NSchema.Aws");
        plugin.Label.ShouldBe("s3");
        plugin.Settings.Attribute("bucket")!.ShouldBe("my-bucket");
    }

    [Fact]
    public async Task State_File_UnknownAttribute_Throws()
        // The built-in file store binds its own attributes, rejecting any it doesn't recognise; other backends defer to the plugin.
        => (await Should.ThrowAsync<InvalidOperationException>(() => Read("STATE file ( badly = 'x' );")))
            .Message.ShouldContain("badly", Case.Insensitive);

    [Fact]
    public async Task Engine_SatisfiedAssertion_Passes()
    {
        var config = await Read("ENGINE ( version = '[5.0,6.0)' );");

        config.Database.ShouldBeNull();
        config.State.ShouldBeNull();
    }

    [Fact]
    public async Task Engine_UnsatisfiedAssertion_Throws()
        => (await Should.ThrowAsync<InvalidOperationException>(() => Read("ENGINE ( version = '[4.0,5.0)' );")))
            .Message.ShouldContain("requires an engine version");

    [Fact]
    public async Task NoConfigurationFile_ReturnsEmpty()
    {
        await File.WriteAllTextAsync(Path.Combine(_directory, "schema.sql"), "CREATE SCHEMA app;", TestContext.Current.CancellationToken);

        var config = await ProjectConfigurationReader.Read(_directory, environment: null, TestContext.Current.CancellationToken);

        config.Database.ShouldBeNull();
        config.State.ShouldBeNull();
    }

    [Fact]
    public async Task MultipleBaseConfigurationFiles_AllLoad()
    {
        await WriteLock(Locked("NSchema.Postgres", "5.0.0"));
        // The .env. marker is a pattern, not a fixed name: every *.env.sql file contributes to the base layer.
        await File.WriteAllTextAsync(Path.Combine(_directory, "plugins.env.sql"),
            "PLUGIN postgres ( source = 'NSchema.Postgres', version = '5.0.0' );", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(_directory, "database.env.sql"),
            "DATABASE postgres ( connection_string = 'host=db' );", TestContext.Current.CancellationToken);

        var config = await ProjectConfigurationReader.Read(_directory, environment: null, TestContext.Current.CancellationToken);

        config.Database!.PackageId.ShouldBe("NSchema.Postgres");
    }

    [Fact]
    public async Task SchemaStatementBesideConfiguration_IsRead()
    {
        // One grammar: declarations and configuration blocks may sit side by side, and the configuration
        // reader takes the statements it understands rather than rejecting the rest.
        await WriteLock(Locked("NSchema.Postgres", "5.0.0-alpha.2"));
        var config = await Read(
            """
            CREATE SCHEMA app;
            PLUGIN postgres ( source = 'NSchema.Postgres', version = '5.0.0-alpha.2' );
            DATABASE postgres ( connection_string = 'host=db' );
            """);

        config.Database!.PackageId.ShouldBe("NSchema.Postgres");
    }

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
        await WriteLock(Locked("NSchema.Aws", "5.0.0"));
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
        config.State.Plugin!.Settings.Attribute("bucket")!.ShouldBe("prod-bucket");
    }

    [Fact]
    public async Task Environment_BaseSliceSurvivesWhenOverlayOmitsIt()
    {
        await WriteLock(Locked("NSchema.Postgres", "5.0.0"));
        // The overlay only declares a state store; the base database carries through unchanged.
        var config = await ReadEnvironment(
            """
            PLUGIN postgres ( source = 'NSchema.Postgres', version = '5.0.0' );
            DATABASE postgres ( connection_string = 'host=base' );
            """,
            "prod",
            "STATE file ( path = './prod.state.json' );");

        config.Database!.Settings.Attribute("connection_string")!.ShouldBe("host=base");
        config.State!.File!.Path.ShouldBe("./prod.state.json");
    }

    [Fact]
    public async Task Environment_NotFound_Throws()
    {
        await File.WriteAllTextAsync(Path.Combine(_directory, "config.env.sql"), "STATE file ( path = './state.json' );", TestContext.Current.CancellationToken);

        (await Should.ThrowAsync<InvalidOperationException>(
                () => ProjectConfigurationReader.Read(_directory, "prod", TestContext.Current.CancellationToken).AsTask()))
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
