using NSchema.Configuration.Ddl;

namespace NSchema.Tests.Configuration.Ddl;

public sealed class DdlProjectConfigReaderTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("nschema-ddlcfg-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private async Task<DdlProjectConfig> Read(string sql)
    {
        await File.WriteAllTextAsync(Path.Combine(_directory, "config.sql"), sql, TestContext.Current.CancellationToken);
        return await DdlProjectConfigReader.Read(_directory, environment: null, TestContext.Current.CancellationToken);
    }

    private async Task<DdlProjectConfig> ReadEnvironment(string baseSql, string environment, string overlaySql)
    {
        await File.WriteAllTextAsync(Path.Combine(_directory, "config.sql"), baseSql, TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(_directory, $"config.env.{environment}.sql"), overlaySql, TestContext.Current.CancellationToken);
        return await DdlProjectConfigReader.Read(_directory, environment, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Provider_ResolvesBuiltInPluginReference()
    {
        var config = await Read("PROVIDER postgres ( version = '4.0.0', connection_string = 'host=db' );");

        var plugin = config.Provider!;
        plugin.PackageId.ShouldBe("NSchema.Postgres");
        plugin.Label.ShouldBe("postgres");
        plugin.Version.ShouldBe("4.0.0");
        // The provider's own attributes survive on the block for the plugin to interpret; version is stripped.
        plugin.Block.Attribute("connection_string")!.AsString().ShouldBe("host=db");
        plugin.Block.Attribute("version").ShouldBeNull();
    }

    [Theory]
    [InlineData("sqlite", "NSchema.Sqlite")]
    [InlineData("sqlserver", "NSchema.SqlServer")]
    public async Task Provider_MapsBuiltInLabelToPackage(string label, string package)
    {
        var config = await Read($"PROVIDER {label} ( version = '4.0.0', connection_string = 'x' );");

        config.Provider!.PackageId.ShouldBe(package);
    }

    [Fact]
    public async Task Provider_Source_OverridesWithThirdPartyPackage()
    {
        var config = await Read("PROVIDER oracle ( source = 'Acme.NSchema.Oracle', version = '1.0.0' );");

        config.Provider!.PackageId.ShouldBe("Acme.NSchema.Oracle");
        config.Provider!.Label.ShouldBe("oracle");
    }

    [Fact]
    public async Task Provider_MissingVersion_Throws()
        => (await Should.ThrowAsync<InvalidOperationException>(() => Read("PROVIDER postgres ( connection_string = 'x' );")))
            .Message.ShouldContain("version");

    [Fact]
    public async Task Provider_UnknownLabelWithoutSource_Throws()
        => (await Should.ThrowAsync<InvalidOperationException>(() => Read("PROVIDER mysql ( version = '1.0.0' );")))
            .Message.ShouldContain("source");

    [Fact]
    public async Task Backend_File_MapsPath()
        => (await Read("BACKEND file ( path = './state.json' );")).State!.File!.Path.ShouldBe("./state.json");

    [Fact]
    public async Task Backend_S3_ResolvesPluginReference()
    {
        var plugin = (await Read("BACKEND s3 ( version = '4.0.0', bucket = 'my-bucket', key = 'state.json' );")).State!.Plugin!;

        plugin.PackageId.ShouldBe("NSchema.Aws");
        plugin.Label.ShouldBe("s3");
        plugin.Block.Attribute("bucket")!.AsString().ShouldBe("my-bucket");
    }

    [Fact]
    public async Task Backend_File_UnknownAttribute_Throws()
        // The built-in file store parses its own attributes; other backends defer attribute validation to the plugin.
        => (await Should.ThrowAsync<InvalidOperationException>(() => Read("BACKEND file ( badly = 'x' );")))
            .Message.ShouldContain("Unknown attribute");

    [Fact]
    public async Task Nschema_Block_IsRejected()
        // The NSCHEMA config block was removed: dialect is the provider's, transaction_mode isn't wired, and the
        // destructive-action policy is a flag / env var — so an NSCHEMA block is now just an unknown block.
        => (await Should.ThrowAsync<InvalidOperationException>(() => Read("NSCHEMA ( dialect = 'postgres' );")))
            .Message.ShouldContain("Unknown configuration block");

    [Fact]
    public async Task ProviderAndBackend_Compose()
    {
        var config = await Read(
            """
            PROVIDER postgres ( version = '4.0.0', connection_string = 'host=db' );
            BACKEND file ( path = './state.json' );
            CREATE SCHEMA app;
            """);

        config.Provider!.Label.ShouldBe("postgres");
        config.State!.File!.Path.ShouldBe("./state.json");
    }

    [Fact]
    public async Task NoConfigBlocks_ReturnsEmpty()
    {
        var config = await Read("CREATE SCHEMA app;");

        config.Provider.ShouldBeNull();
        config.State.ShouldBeNull();
    }

    [Fact]
    public async Task UnknownBlockType_Throws()
        => (await Should.ThrowAsync<InvalidOperationException>(() => Read("WORKSPACE staging ( region = 'eu' );")))
            .Message.ShouldContain("Unknown configuration block");

    [Fact]
    public async Task DuplicateProvider_Throws()
        => (await Should.ThrowAsync<InvalidOperationException>(() =>
                Read("PROVIDER postgres ( version = '4.0.0' ); PROVIDER postgres ( version = '4.0.0' );")))
            .Message.ShouldContain("More than one");

    // ── Environment overlays ──────────────────────────────────────────────────

    [Fact]
    public async Task Environment_OverlayReplacesBaseSlice()
    {
        // The base uses a file backend; the prod overlay switches it to S3, replacing the slice wholesale.
        var config = await ReadEnvironment(
            "BACKEND file ( path = './state.json' );",
            "prod",
            "BACKEND s3 ( version = '4.0.0', bucket = 'prod-bucket', key = 'state.json' );");

        config.State!.File.ShouldBeNull();
        config.State.Plugin!.Block.Attribute("bucket")!.AsString().ShouldBe("prod-bucket");
    }

    [Fact]
    public async Task Environment_BaseSliceSurvivesWhenOverlayOmitsIt()
    {
        // The overlay only declares a backend; the base provider carries through unchanged.
        var config = await ReadEnvironment(
            "PROVIDER postgres ( version = '4.0.0', connection_string = 'host=base' );",
            "prod",
            "BACKEND file ( path = './prod.state.json' );");

        config.Provider!.Block.Attribute("connection_string")!.AsString().ShouldBe("host=base");
        config.State!.File!.Path.ShouldBe("./prod.state.json");
    }

    [Fact]
    public async Task Environment_NotFound_Throws()
    {
        await File.WriteAllTextAsync(Path.Combine(_directory, "config.sql"), "CREATE SCHEMA app;", TestContext.Current.CancellationToken);

        (await Should.ThrowAsync<InvalidOperationException>(
                () => DdlProjectConfigReader.Read(_directory, "prod", TestContext.Current.CancellationToken).AsTask()))
            .Message.ShouldContain("environment 'prod'");
    }

    [Fact]
    public async Task Environment_Null_IgnoresOverlayFiles()
    {
        // An overlay file is present, but with no environment selected the base config must not read its blocks.
        await File.WriteAllTextAsync(Path.Combine(_directory, "config.env.prod.sql"),
            "BACKEND s3 ( version = '4.0.0', bucket = 'prod-bucket', key = 'state.json' );", TestContext.Current.CancellationToken);

        var config = await Read("CREATE SCHEMA app;");

        config.State.ShouldBeNull();
    }
}
